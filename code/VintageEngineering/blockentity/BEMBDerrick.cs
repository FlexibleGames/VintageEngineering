using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using VintageEngineering.GUI;
using VintageEngineering.Multiblock;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    /// <summary>
    /// Derrick finishes the extraction process by pumping out the remaining material and placing the well casing blocks down to the well source block.<br/>
    /// Inventory slot 0 = item, 1 = fluid
    /// </summary>
    public class BEMBDerrick : VEMBEntityCore, IVELiquidInterface
    {
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        /// <summary>
        /// Code of the expected and required Well Casing block.
        /// </summary>
        private string _wellCasingCode = string.Empty;
        /// <summary>
        /// Code of the fluid this Derrick will look for, set with whatever fluid<br/>
        /// is directly below the Derrick Core block. Ensures Water or Lava does not<br/>
        /// contaminate the process.
        /// </summary>
        private string _wellFluidBlockCode = string.Empty;
        private string _wellFluidCode = string.Empty;
        private int _sourceBlocksPerSecond = 0;
        private float _wellValidationDelay = 0f;
        private float _lastPumpDelta = 0f;

        /// <summary>
        /// Represents the current Y position the Derrick is at, not the position of the well block.<br/>
        /// For this machine, this value should be at the center of the Derrick, but at a deeper Y level<br/>
        /// Y should always be less than machines Y level.
        /// </summary>
        private BlockPos _wellPosition = null;
        private bool _wellCompleted = false;
        /// <summary>
        /// List of all positions for the current layer being cleared of fluid, includes Distance value for sorting.<br/>
        /// Not saved or synced. Rebuilt on load based on _wellPosition as origin.
        /// </summary>
        private List<BlockPosAndDist> _currentLayer = new List<BlockPosAndDist>();

        #region InitAndMisc 
        public BEMBDerrick()
        {
            float capacity = 8001f;
            _inventory = new InventoryGeneric(2, null, null, delegate (int id, InventoryGeneric self)
            {
                if (id == 0) return new ItemSlot(self);
                return new ItemSlotLiquidOnly(self, capacity);
            });
            _inventory.SlotModified += this.SlotModified;
            _inventory.OnGetSuitability += this.GetSuitability;
            _inventory.OnGetAutoPushIntoSlot += this.GetAutoPushIntoSlot;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            float powerpercent = 0f;
            if (Electric.MaxPower > 0) powerpercent = (float)((double)Electric.CurrentPower / (double)Electric.MaxPower);
            int percentpower = (int)(powerpercent * 100);

            base.GetBlockInfo(forPlayer, dsc);
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-fluid")}: {IconHelper.PercentToBar(PercentFluid, 10)} {PercentFluid:N0}%");
            dsc.AppendLine($"{Lang.Get("vinteng:gui-word-power")}: {IconHelper.PercentToBar(percentpower, 10)} {percentpower:N0}%");
            if (_wellPosition != null)
            {
                int depth = this.Pos.Y - _wellPosition.Y;
                dsc.AppendLine(Lang.Get("vinteng:gui-drillingat") + $" {depth:N0} " + Lang.Get("vinteng:gui-word-depth"));                
            }
            else
            {
                dsc.AppendLine($"{Lang.Get("vinteng:gui-wellerror")}");
            }
            if (InputSlot.Empty || InputSlot.Itemstack.Collectible.Code != _wellCasingCode)
            {
                dsc.AppendLine(Lang.Get("vinteng:gui-missingcasing"));
            }
            else
            {
                dsc.AppendLine($"{Lang.Get("vinteng:block-wellcasing")} : {InputSlot.Itemstack.StackSize}");
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _wellCasingCode = base.Block.Attributes["wellCasingCode"].AsString();
            _inventory.Pos = this.Pos;
            _inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            (_inventory[1] as ItemSlotLiquidOnly).CapacityLitres = base.Block.Attributes["fluidCapacityLiters"].AsFloat(1f);

            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 500, 0);
                _sourceBlocksPerSecond = base.Block.Attributes["sourceBlocksPerSecond"].AsInt(1);
                //if (_wellPosition == null && _wellCompleted == false) // first time initializing (aka just built)
                //{
                //    // there are, of course, many edge cases why position is null here
                //    // but the tick will revalidate every 120 seconds
                //    // maybe I should trigger it on the interaction block instead?
                //    if (!InputSlot.Empty && InputSlot.Itemstack.Collectible.Code == _wellCasingCode)
                //    {
                //        ValidateWell(); 
                //    }
                //}
                if (_wellPosition != null)
                {
                    // TODO: Rebuild Fluid Layer Pump list
                }
                _wellValidationDelay = 0f;
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("vembderrick", null, null, new Vec3f(0f, GetRotation(), 0f));
                }
            }     
        }

        /// <summary>
        /// What is the BlockPos of the Derrick head? Must be over the Well Casing that leads down to the well.
        /// </summary>
        public BlockPos HeadPosition
        {
            get
            {                
                return this.Pos.Copy();
            }
        }

        public BlockPos WellPosition => _wellPosition;

        public int GetRotation()
        {
            string side = base.Block.Variant["side"];
            int adjustedIndex = ((BlockFacing.FromCode(side)?.HorizontalAngleIndex ?? 1) + 3) & 3;
            return adjustedIndex * 90;
        }
        #endregion

        public void OnSimTick(float dt)
        {
            if (sapi == null) return; // only run this on the server

            if (base.Block.Variant["state"] != "built") return;
            
            EnumBEState newstate = MachineState;

            if (newstate == EnumBEState.Off) return;
            if (_wellCompleted) return;
            else
            {
                // casing needed before we start/continue
                if (InputSlot.Empty) return;
                if (InputSlot.Itemstack.Collectible.Code != _wellCasingCode) return;
            }
            
            _wellValidationDelay += dt;

            if (_wellValidationDelay >= 120f)
            {
                // revalidate every 2 minutes
                if (!ValidateWell())
                {
                    // if the well is invalid, shut it down
                    newstate = EnumBEState.Sleeping;
                }
                else
                {
                    // well is valid, but we may have to wake up
                    if (MachineState == EnumBEState.Sleeping || MachineState == EnumBEState.Off)
                    {
                        newstate = EnumBEState.On;
                    }
                }
                _wellValidationDelay = 0f;
            }
            if (Electric.CurrentPower < Electric.RatedPower(dt, false))
            {
                // if we're supposed to be on, but we don't have enough power, sleep
                if (newstate == EnumBEState.On) newstate = EnumBEState.Sleeping;
            }
            else 
            {
                newstate = EnumBEState.On; // power is green
            }

            if (newstate == EnumBEState.On)
            {
                if (_wellPosition == null) // typically the first time the derrick is powered
                {
                    _wellPosition = GetFirstPumpablePosition(HeadPosition.DownCopy());
                    if (_wellPosition == null) return;
                }
                // we have enough power and a valid fluid! \o/
                if (_currentLayer.Count == 0)
                {
                    // grab all valid source blocks at or above current pos
                    BuildPumpableFluidLayer(_wellPosition);
                }
                if (_currentLayer.Count == 0) return; // still nothing to pump...

                Block fluidblock = sapi.World.GetBlock(new AssetLocation(_wellFluidBlockCode));
                ItemStack portionstack = new ItemStack(fluidblock);
                WaterTightContainableProps bprops = BlockLiquidContainerBase.GetContainableProps(portionstack);

                if (bprops == null) return; // odd, we have a fluid, but no props?

                int literspersecond = 1000; // _sourceBlocksPerSecond * 1000;
                int portionperliter = 100;

                _lastPumpDelta += dt * _sourceBlocksPerSecond;
                if (_lastPumpDelta < 1f) return;

                Item portion = Api.World.GetItem(new AssetLocation(bprops.WhenFilled.Stack.Code));
                if (portion != null)
                {
                    ItemStack itemportionstack = new ItemStack(portion);
                    WaterTightContainableProps iprops = BlockLiquidContainerBase.GetContainableProps(itemportionstack);
                    if (iprops != null)
                    {
                        // this is an insane chain of things I have to do just to get the damn props
                        portionperliter = ((int)iprops.ItemsPerLitre); // almost always 100, should be 1000 for milliliters. 
                    }
                }
                else return;
                // a truly crazy way of turning blocks/s of source fluid into portions/s
                // by default 1 block/s * 1000 * 100 = 100,000 portions per second
                long portionpersecond = literspersecond * portionperliter;
                double availportion = Output.CapacityLitres * portionperliter; // 100 portions per liter is the standard... default to this, if it's empty this is the available by default.
                if (!_inventory[0].Empty) availportion = Output.CapacityLitres * BlockLiquidContainerBase.GetContainableProps(Output.Itemstack).ItemsPerLitre - Output.Itemstack.StackSize;

                if (availportion < portionpersecond) return; // we do not have enough space for another pump action

                // if we are here, we are ready for a pump event
                BlockPosAndDist nextone = _currentLayer.First();

                if (!Output.Empty) Output.Itemstack.StackSize += (int)portionpersecond;
                else
                {
                    ItemStack fluidstack = new ItemStack(portion, (int)portionpersecond);
                    Output.Itemstack = fluidstack;
                }
                sapi.World.BlockAccessor.SetBlock(0, nextone.Pos);
                _lastPumpDelta = 0f;

                if (_currentLayer.Count == 1)
                {
                    // we are on the very last fluid block to pump
                    _currentLayer.Clear();
                    _wellPosition.Down(1);
                    Block casing = sapi.World.GetBlock(new AssetLocation(_wellCasingCode));
                    sapi.World.BlockAccessor.SetBlock(casing.Id, nextone.Pos);
                }
                else
                {
                    _currentLayer.Remove(nextone);
                }
                Electric.electricpower -= Electric.RatedPower(dt, false);

            }
            if (MachineState != newstate) SetState(newstate);

            _clientUpdateDelay += dt;
            if (_clientUpdateDelay > 0.5)
            {
                _clientUpdateDelay = 0f;
                MarkDirty(true);
            }
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            // force a well validation check
            if (Api.Side == EnumAppSide.Server) _wellValidationDelay = 121f;
            return true;
        }

        /// <summary>
        /// Validates the Well. _wellPosition will be set after this call if there is one and the well is valid.
        /// </summary>
        /// <returns>True if well is valid and ready for pumpin'</returns>
        public bool ValidateWell()
        {
            if (base.Block.Variant["state"] != "built") return false;

            AssetLocation casingcode = new AssetLocation(base.Block.Attributes["wellCasingCode"].AsString());
            Block casing = Api.World.GetBlock(casingcode);
            if (casing == null) return false; // if casing can't be found, bounce

            // TODO: Place Casing in Air as well is validated

            if (_wellPosition == null)
            {
                // Derrick was just built, grab the fluid it should be pumping
                BlockPos below = HeadPosition.AddCopy(BlockFacing.DOWN);
                Block check = Api.World.BlockAccessor.GetBlock(below);
                if (check.IsLiquid()) 
                {
                    if (_wellFluidBlockCode == string.Empty)
                    {
                        _wellFluidBlockCode = check.Code.ToString();
                        _wellFluidCode = check.LiquidCode;
                    }
                    _wellPosition = below.Copy();
                }
                else
                {
                    if (check.Id != 0)
                    {
                        // block under Center of Derrick is not a liquid and not air
                        if (check == casing)
                        {
                            _wellPosition = GetFirstPumpablePosition(below);
                            if (_wellPosition == null && _wellCompleted == false) return false;
                            else return true;
                        }
                        else
                        {
                            _wellPosition = null;
                        }
                    }
                    else
                    {
                        // block under Center of Derrick is air
                        _wellPosition = GetFirstPumpablePosition(below);
                        if (_wellPosition == null && _wellCompleted == false) return false;
                        else return true;
                    }
                }
            }
            Block atpos = Api.World.BlockAccessor.GetBlock(_wellPosition);
            if (!atpos.IsLiquid()) return false; // if liquid isn't at our current position, bounce
            return true;
        }

        /// <summary>
        /// Validates all Well Casing blocks from the surface down to the well.<br/>
        /// If the well exists, this will also set the _wellPosition value.<br/>
        /// Returns false if either the casing is broken or the well isn't found.
        /// </summary>
        /// <param name="start">Position to start in. Use Copy() when passing into this function as the position will change.</param>
        /// <returns>True if casing is valid and well is found, otherwise false.</returns>
        public bool ValidateCasings(BlockPos start)
        {
            Block casing = Api.World.GetBlock(new AssetLocation(base.Block.Attributes["wellCasingCode"].AsString()));

            while (start.Y > 0)
            {
                Block blockat = Api.World.BlockAccessor.GetBlock(start.Down());
                if (blockat == casing) continue;
                else
                {
                    if (Api.World.BlockAccessor.GetBlockEntity(start) is IFluidWell)
                    {
                        _wellPosition = start.Copy();
                        return true;
                    }
                }
            }
            return false;
        }
        /// <summary>
        /// From Start check down to find the first position that has a valid fluid block to pump, <br/>
        /// if fluid filter is set, only validates that fluid. If well is hit _wellCompleted is set to true.
        /// </summary>
        /// <param name="start">Starting point, Y value of this WILL change.</param>
        /// <returns>BlockPos if found, null if not found or well completed.</returns>
        public BlockPos GetFirstPumpablePosition(BlockPos start)
        {
            if (Api == null || sapi == null) return null;
            Block casing = Api.World.GetBlock(new AssetLocation(base.Block.Attributes["wellCasingCode"].AsString()));
            while (start.Y > 0)
            {
                // TODO: Place Casing as search finds air or invalid (not matching filter) fluids
                Block blockat = sapi.World.BlockAccessor.GetBlock(start.Down());
                if (blockat == casing) continue;
                else
                {
                    if (blockat.Id == 0) 
                    {
                        // we ran out of casing or we have invalid casing, bounce
                        if (InputSlot.Empty || InputSlot.Itemstack.Collectible.Code != _wellCasingCode) return null;

                        // set the casing and move on
                        sapi.World.BlockAccessor.SetBlock(casing.Id, start);
                        this._inventory[1].TakeOut(1);
                        continue; 
                    }
                    if (blockat.IsLiquid())
                    {
                        if (_wellFluidBlockCode == string.Empty) 
                        {
                            _wellFluidBlockCode = blockat.Code;
                            _wellFluidCode = blockat.LiquidCode;
                            return start; 
                        }
                        if (blockat.Code == _wellFluidBlockCode || blockat.LiquidCode == _wellFluidCode) return start;
                        else 
                        {
                            // set the casing and move on
                            sapi.World.BlockAccessor.SetBlock(casing.Id, start);
                            this._inventory[1].TakeOut(1);
                            continue; // its a fluid, but not the one we want, skip
                        }
                    }
                    else
                    {
                        if (sapi.World.BlockAccessor.GetBlockEntity(start) is IFluidWell)
                        {
                            _wellCompleted = true;
                            return null;
                        }
                        else 
                        {
                            _wellCompleted = false; // we ran into a solid block that isn't liquid, air, casing, or a well
                            return null; 
                        }
                    }
                }
            }
            return null;
        }


        /// <summary>
        /// From the given start position (origin) determine what fluid blocks are available to the machine. <br/>
        /// Builds the _currentLayer variable if blocks are found. _currentLayer.Count > 0 if we are ready to pump!
        /// </summary>
        /// <param name="start">Origin point to check from.</param>
        /// <returns>true if successful</returns>
        public bool BuildPumpableFluidLayer(BlockPos start)
        {
            if (sapi == null) return false;

            Block startblock = sapi.World.BlockAccessor.GetBlock(start);
            if (!startblock.IsLiquid()) return false;

            List<BlockPos> tocheck = new List<BlockPos>();
            List<BlockPos> checkcache = new List<BlockPos>();

            Dictionary<BlockPos, float> arevalid = new Dictionary<BlockPos, float>();

            tocheck.Add(start.Copy());

            List<BlockPos> toadd = new List<BlockPos>();

            while (tocheck.Count > 0 && arevalid.Count < 1000)
            {                                 
                foreach (BlockPos bpos in tocheck)
                {
                    BlockPos startpos = bpos.AddCopy(-1, 0, -1);
                    BlockPos endpos = bpos.AddCopy(1, 1, 1);
                    sapi.World.BlockAccessor.WalkBlocks(startpos, endpos, delegate (Block dblock, int x, int y, int z)
                    {
                        if (arevalid.Count > 1000) return;
                        if (dblock.Id != 0 && dblock.IsLiquid() && dblock.LiquidCode == _wellFluidCode)
                        {
                            BlockPos pendingpos = new BlockPos(x, y, z, start.dimension);
                            float dist = start.DistanceTo(pendingpos);
                            dist *= Math.Abs(pendingpos.Y - start.Y) + 1;

                            if (!arevalid.ContainsKey(pendingpos))
                            {
                                if (dblock.LiquidLevel == 7)
                                {
                                    arevalid.Add(pendingpos.Copy(), dist);
                                    toadd.Add(pendingpos);
                                }
                                else
                                {
                                    if (!checkcache.Contains(pendingpos))
                                    {
                                        checkcache.Add(pendingpos);
                                        toadd.Add(pendingpos);
                                    }
                                }
                            }
                        }
                    }, false);
                }
                tocheck.Clear();
                if (toadd.Count > 0 && arevalid.Count < 1000)
                {
                    tocheck.AddRange(toadd);
                }
                toadd.Clear();
            }
            checkcache.Clear();
            if (_currentLayer.Count > 0) _currentLayer.Clear();

            if (arevalid.Count > 0)
            {                
                foreach (KeyValuePair<BlockPos, float> entry in arevalid)
                {
                    _currentLayer.Add(new BlockPosAndDist(entry.Key.Copy(), entry.Value));
                }
                _currentLayer.Sort((x, y) => y.Distance.CompareTo(x.Distance));
            }
            arevalid.Clear();
            return true;
        }

        public IFluidWell GetWellAt(BlockPos pos)
        {
            return Api.World.BlockAccessor.GetBlockEntity(pos) as IFluidWell;
        }

        #region InventoryStuff
        public virtual bool AllowPipeLiquidTransfer
        {
            get
            {
                if (base.Block.Attributes == null) return false;
                return base.Block.Attributes["allowPipeLiquidTransfer"].AsBool(false);
            }
        }

        public virtual bool AllowHeldLiquidTransfer
        {
            get
            {
                if (base.Block.Attributes == null) return false;
                return base.Block.Attributes["allowHeldLiquidTransfer"].AsBool(false);
            }
        }

        public virtual float TransferSizeLitresPerSecond
        {
            get
            {
                if (base.Block.Attributes == null) return 0f;
                return base.Block.Attributes["transferLitresPerSecond"].AsFloat(0.01f);
            }
        }
        /// <summary>
        /// SlotID 0 = Item input, 1 = fluid output
        /// </summary>
        private InventoryGeneric _inventory;
        public override string InventoryClassName => "InvDerrick";
        public override InventoryBase Inventory => _inventory;

        public ItemSlot OutputSlot => _inventory[1];
        public ItemSlotLiquidOnly Output => OutputSlot as ItemSlotLiquidOnly;

        public ItemSlot InputSlot => _inventory[0];

        public int[] InputLiquidContainerSlotIDs => null;

        public int[] OutputLiquidContainerSlotIDs => [1];

        /// <summary>
        /// How full is the internal tank? 0-100
        /// </summary>
        public int PercentFluid
        {
            get
            {
                if (_inventory[0].Empty) return 0;
                else
                {
                    int portions = _inventory[0].Itemstack.StackSize;
                    float capacity = (_inventory[0] as ItemSlotLiquidOnly).CapacityLitres * BlockLiquidContainerBase.GetContainableProps(_inventory[0].Itemstack).ItemsPerLitre;
                    float full = (float)portions / capacity;
                    return (int)(full * 100);
                }
            }
        }
        /// <summary>
        /// Required for Pipe & Chute interaction. Should return the ItemSlot if the fromSlot is NOT empty or a liquid.
        /// </summary>
        /// <param name="face">Face being checked</param>
        /// <param name="fromSlot">What is looking to be pushed into this</param>
        /// <returns>ItemSlot</returns>
        public ItemSlot GetAutoPushIntoSlot(BlockFacing face, ItemSlot fromSlot)
        {
            if (fromSlot.Empty || fromSlot.Itemstack.Collectible.IsLiquid()) return null;

            return _inventory[0];
        }

        public ItemSlotLiquidOnly GetLiquidAutoPushIntoSlot(BlockFacing blockFacing, ItemSlot fromSlot = null)
        {
            // you can not push liquid into this machine.
            return null;
        }

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            string rotside = base.Block.Variant["side"];

            // if this is facing North, the south face is the fluid output
            string opposite = BlockFacing.FromCode(rotside).Opposite.Code;            
            if (blockFacing.Code != opposite) return null;

            return Output;
        }

        private void SlotModified(int slotid)
        {
            if (slotid <= 1) _clientUpdateDelay += 0.025f;
        }

        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid > 1 || slotid < 0) return false;
            if (slotid == 1)
            {
                if (_inventory[slotid].Empty) return true;
                float capportion = (_inventory[1] as ItemSlotLiquidOnly).CapacityLitres * BlockLiquidContainerBase.GetContainableProps(_inventory[1].Itemstack).ItemsPerLitre;
                if (_inventory[slotid].Itemstack.StackSize == capportion) return false;
                return true;
            }
            else
            {
                if (_inventory[slotid].Empty) return true;
                if (_inventory[slotid].Itemstack.StackSize < _inventory[slotid].Itemstack.Collectible.MaxStackSize) return true;
            }
                
            return false;
        }

        public float GetSuitability(ItemSlot sourceslot, ItemSlot targetSlot, bool isMerge)
        {
            return (isMerge ? (_inventory.BaseWeight + 3f) : (_inventory.BaseWeight + 1f)) + ((sourceslot.Inventory is InventoryBasePlayer) ? 1 : 0);
        }

        #endregion

        #region MachineState
        private EnumBEState _state;
        public EnumBEState MachineState => _state;
        public bool IsSleeping => _state == EnumBEState.Sleeping;
        public bool IsActive => _state == EnumBEState.On;

        protected virtual void SetState(EnumBEState newstate)
        {
            _state = newstate;
            if (_state == EnumBEState.On)
            {
                if (AnimUtil != null && base.Block.Attributes["craftinganimcode"].Exists)
                {
                    AnimUtil.StartAnimation(new AnimationMetaData
                    {
                        Animation = base.Block.Attributes["craftinganimcode"].AsString(),
                        Code = base.Block.Attributes["craftinganimcode"].AsString(),
                        AnimationSpeed = 2f,
                        EaseOutSpeed = 4f,
                        EaseInSpeed = 1f
                    });
                }
            }
            else
            {
                if (AnimUtil != null && AnimUtil.activeAnimationsByAnimCode.Count > 0)
                {
                    AnimUtil.StopAnimation(base.Block.Attributes["craftinganimcode"].AsString());
                }
            }
            MarkDirty(true); // _clientUpdateDelay += 0.05f; 
        }
        #endregion

        #region TreeAttributes
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            _inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            if (_wellFluidBlockCode != string.Empty) tree.SetString("wellfluidblock", _wellFluidBlockCode);
            if (_wellFluidCode != string.Empty) tree.SetString("wellfluid", _wellFluidCode);
            if (_wellPosition != null) tree.SetBlockPos("wellposition", _wellPosition);
            tree.SetBool("wellcomplete", _wellCompleted);
            tree.SetString("machinestate", MachineState.ToString());
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            _wellFluidBlockCode = tree.GetString("wellfluidblock", string.Empty);
            _wellFluidCode = tree.GetString("wellfluid", string.Empty);
            _wellCompleted = tree.GetBool("wellcomplete", false);
            BlockPos syncwellPosition = tree.GetBlockPos("wellposition", null);
            if (syncwellPosition != null && _wellPosition != syncwellPosition)
            {
                _wellPosition = syncwellPosition;
                // TODO: Build Layer Fluid List
            }

            EnumBEState syncstate = Enum.Parse<EnumBEState>(tree.GetString("machinestate", "On"));
            if (MachineState != syncstate) SetState(syncstate);
        }
        #endregion
    }
}
