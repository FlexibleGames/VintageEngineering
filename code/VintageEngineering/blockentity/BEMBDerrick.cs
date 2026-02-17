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
        private int _sourceBlocksPerSecond = 0;
        private float _wellValidationDelay = 0f;

        /// <summary>
        /// Represents the current Y position the Derrick is at, not the position of the well block.<br/>
        /// For this machine, this value should be at the center of the Derrick, but at a deeper Y level<br/>
        /// Y should always be less than machines Y level.
        /// </summary>
        private BlockPos _wellPosition = null;
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
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 500, 0);
                _sourceBlocksPerSecond = base.Block.Attributes["sourceBlocksPerSecond"].AsInt(1);
                if (_wellPosition == null) // first time initializing (aka just built)
                {
                    // there are, of course, many edge cases why position is null here
                    // but the tick will revalidate every 120 seconds
                    // maybe I should trigger it on the interaction block instead?
                    ValidateWell();
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
            _inventory.Pos = this.Pos;
            _inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            (_inventory[1] as ItemSlotLiquidOnly).CapacityLitres = base.Block.Attributes["fluidCapacityLiters"].AsFloat(1f);
            _wellCasingCode = base.Block.Attributes["wellCasingCode"].AsString();
        }

        /// <summary>
        /// What is the BlockPos of the Pumpjack head? Must be over the Well Casing that leads down to the well.
        /// </summary>
        public BlockPos HeadPosition
        {
            get
            {
                string side = base.Block.Variant["side"] ?? "north";
                BlockFacing pointingto = BlockFacing.FromCode(side);
                return this.Pos.AddCopy(pointingto, 2);
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

            _wellValidationDelay += dt;
            EnumBEState newstate = MachineState;

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
            else newstate = EnumBEState.On; // power is green

            if (newstate == EnumBEState.On)
            {
                // we have enough power and a valid well! \o/
                int literspersecond = _sourceBlocksPerSecond * 1000;
                int portionperliter = 100;
                IFluidWell thewell = GetWellAt(_wellPosition);
                Item portion = Api.World.GetItem(new AssetLocation(thewell?.FluidPortionCode));
                if (portion != null)
                {
                    ItemStack portionstack = new ItemStack(portion);
                    WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(portionstack);
                    if (props != null)
                    {
                        // this is an insane chain of things I have to do just to get the damn props
                        portionperliter = ((int)props.ItemsPerLitre); // almost always 100, should be 1000 for milliliters. 
                    }
                }
                // a truly crazy way of turning blocks/s of source fluid into portions/s
                // by default 1 block/s * 1000 * 100 = 100,000 portions per second
                long portionpersecond = literspersecond * portionperliter;
                double availportion = Output.CapacityLitres * 100; // 100 portions per liter is the standard... default to this, if it's empty this is the available by default.
                if (!_inventory[0].Empty) availportion = Output.CapacityLitres * BlockLiquidContainerBase.GetContainableProps(Output.Itemstack).ItemsPerLitre - _inventory[0].Itemstack.StackSize;

                portionpersecond = Math.Min(portionpersecond, (long)availportion);

                long texastea = thewell.PumpTick(dt, portionpersecond);
                if (!_inventory[0].Empty) _inventory[0].Itemstack.StackSize += ((int)texastea);
                else
                {
                    _inventory[0].Itemstack = new ItemStack(portion, ((int)texastea));
                }
                if (texastea > 0) Electric.electricpower -= Electric.RatedPower(dt, false);

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
            AssetLocation casingcode = new AssetLocation(base.Block.Attributes["wellCasingCode"].AsString());
            Block casing = Api.World.GetBlock(casingcode);
            if (casing == null) return false; // if casing can't be found, bounce
            if (Api.World.BlockAccessor.GetBlock(HeadPosition.DownCopy()) != casing) return false; // if casing isn't under the head, bounce

            // this validates all casings down to the well, it also sets the well position if found
            if (!ValidateCasings(HeadPosition.DownCopy())) return false;

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
            if (_wellFluidBlockCode != string.Empty) tree.SetString("wellfluid", _wellFluidBlockCode);
            if (_wellPosition != null) tree.SetBlockPos("wellposition", _wellPosition);
            tree.SetString("machinestate", MachineState.ToString());
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            _wellFluidBlockCode = tree.GetString("wellfluid", string.Empty);
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
