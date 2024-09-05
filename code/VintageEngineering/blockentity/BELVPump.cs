using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using VintageEngineering.Electrical;
using VintageEngineering.Transport.API;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering.blockentity
{
    public struct FluidPosition : IEquatable<FluidPosition>, IEquatable<BlockPos>
    {
        public BlockPos Position;
        public int Distance;
        public FluidPosition(BlockPos _position, int _distance)
        {
            this.Position = _position;
            this.Distance = _distance;
        }

        public bool Equals(FluidPosition other)
        {
            return Position == other.Position && Distance == other.Distance;
        }

        public bool Equals(BlockPos other)
        {
            return Position == other;
        }
    }

    public class BELVPump: BlockEntity, IVELiquidInterface, IBlockEntityContainer
    {
        protected InventoryGeneric inventory;
        protected ICoreClientAPI capi;
        protected ICoreServerAPI sapi;
        private float _clientupdatedelay = 0f;
        private string _fluidtype;
        private List<FluidPosition> _fluidpositions;
        private bool _ischeckingfluid = true;
        private bool _isinfinite = false;
        private int _pumpcount = 0; // used only on client

        private int _powerPerBlockPumped = 200;
        private int _powerPerTankPush = 50;

        public ItemSlotLargeLiquid Tank => inventory[0] as ItemSlotLargeLiquid;

        protected BlockEntityAnimationUtil AnimUtil
        {
            get
            {
                return Electric.AnimUtil;
            }
        }
        public string InventoryClassName { get { return "VELVPump"; } }

        public ElectricBEBehavior Electric { get; private set; }

        public int[] InputLiquidContainerSlotIDs => null;

        public int[] OutputLiquidContainerSlotIDs => new[]{ 0 };

        public bool AllowPipeLiquidTransfer => true;

        public bool AllowHeldLiquidTransfer => false;

        public float TransferSizeLitresPerSecond => 50;

        public IInventory Inventory => inventory;

        public BELVPump ()
        {
            inventory = new InventoryGeneric(1, null, null, delegate (int id, InventoryGeneric self)
            {
                return new ItemSlotLargeLiquid(self, 2000);
            });
            inventory.BaseWeight = 1;
            inventory.SlotModified += OnSlotModified;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
                RegisterGameTickListener(new Action<float>(OnSimTick), 250, 0);
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("velvpump", null, null, new Vec3f(0f, 0f, 0f));
                }
            }
            inventory.Pos = this.Pos;
            inventory.LateInitialize($"{InventoryClassName}-{this.Pos.X}/{this.Pos.Y}/{this.Pos.Z}", api);
            _powerPerBlockPumped = base.Block.Attributes["powerperblockpumped"].AsInt(200);
            _powerPerTankPush = base.Block.Attributes["powerpertankpush"].AsInt(50);
            if (api.Side == EnumAppSide.Server) 
            {
                if (CheckForFluid() && !_isinfinite) TyronThreadPool.QueueTask(GetFluids, "VELVPump");
                else _ischeckingfluid = false;
            }
        }

        public void OnSimTick(float dt)
        {
            _clientupdatedelay += dt;
            if (_ischeckingfluid || _fluidpositions == null) return;
            if (_clientupdatedelay > 10) _clientupdatedelay = 0;
            if (_fluidpositions.Count == 0 && !_isinfinite) 
            {
                // we are out of blocks to pump... sleep now
                SetState(EnumBEState.Sleeping);
                return; 
            }
            // we don't have enough power
            if (Electric.CurrentPower < ((ulong)_powerPerBlockPumped))
            {
                SetState(EnumBEState.Paused);
                return; 
            }
            else
            {
                if (Electric.MachineState == EnumBEState.Paused)
                {
                    SetState(EnumBEState.On);
                }
            }

            FluidPosition last = _isinfinite ? new FluidPosition(Pos.DownCopy(1), 0) : _fluidpositions.Last<FluidPosition>();

            if (!BEPipeBase.IsChunkLoaded(Api.World, last.Position))
            {
                if (_isinfinite) return; // the one block we look at is right below the pump and it isn't loaded... this shouldn't be possible
                _fluidpositions.Remove(last);
                while (true)
                {
                    last = _fluidpositions.Last<FluidPosition>();
                    if (BEPipeBase.IsChunkLoaded(Api.World, last.Position)) break;
                    else 
                    { 
                        _fluidpositions.Remove(last);
                        if (_fluidpositions.Count == 0) break;
                    }
                }
            }

            if (Api.World.BlockAccessor.GetChunk(last.Position.X / GlobalConstants.ChunkSize, 
                                                 last.Position.Y / GlobalConstants.ChunkSize, 
                                                 last.Position.Z / GlobalConstants.ChunkSize) == null) return;

            if (Tank.Itemstack != null)
            {
                // tank is not empty
                WaterTightContainableProps props = GetWPropsFromPos(Api.World, last.Position);
                if (props != null)
                {
                    int literperblock = 1000;// ((int)(1.0f / props.ItemsPerLitre));

                    if (props.WhenFilled.Stack.ResolvedItemstack == null)
                    {
                        props.WhenFilled.Stack.Resolve(Api.World, "VELVpump", true);
                    }

                    WaterTightContainableProps portionprops = BlockLiquidContainerBase.GetContainableProps(props.WhenFilled.Stack.ResolvedItemstack);

                    if (portionprops != null)
                    {
                        float portionperliter = portionprops.ItemsPerLitre;

                        float portionperblock = literperblock * portionperliter;

                        if (Tank.Itemstack.StackSize <= portionperblock)
                        {
                            // internal tank has room for another 'block' of fluid
                            if (Electric.MachineState != EnumBEState.On) SetState(EnumBEState.On);
                            
                            Tank.Itemstack.StackSize += (int)portionperblock; // add fluid to tank
                            if (!_isinfinite)
                            {
                                Api.World.BlockAccessor.SetBlock(0, last.Position, BlockLayersAccess.Fluid);
                                Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(last.Position);
                                _fluidpositions.Remove(last);
                            }
                            Electric.electricpower -= ((ulong)_powerPerBlockPumped);
                            MarkDirty(true);
                        }
                    }
                }
            }
            else
            {
                // tank is empty
                WaterTightContainableProps props = GetWPropsFromPos(Api.World, last.Position);
                if (props != null)
                {
                    int literperblock = 1000;// ((int)(1.0f / props.ItemsPerLitre));

                    if (props.WhenFilled.Stack.ResolvedItemstack == null)
                    {
                        props.WhenFilled.Stack.Resolve(Api.World, "VELVpump", true);
                    }

                    WaterTightContainableProps portionprops = BlockLiquidContainerBase.GetContainableProps(props.WhenFilled.Stack.ResolvedItemstack);

                    if (portionprops != null)
                    {
                        float portionperliter = portionprops.ItemsPerLitre;

                        float portionperblock = literperblock * portionperliter;

                        ItemStack blockportion = props.WhenFilled.Stack.ResolvedItemstack;
                        blockportion.StackSize = (int)portionperblock;
                        (inventory[0] as ItemSlotLargeLiquid).SetCapacity(2000, (int)portionperliter);

                        inventory[0].Itemstack = blockportion;
                        
                        if (!_isinfinite)
                        {
                            Api.World.BlockAccessor.SetBlock(0, last.Position, BlockLayersAccess.Fluid);
                            Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(last.Position);
                            _fluidpositions.Remove(last);
                        }
                        if (Electric.MachineState != EnumBEState.On) SetState(EnumBEState.On);
                        Electric.electricpower -= ((ulong)_powerPerBlockPumped);
                        MarkDirty(true);
                    }
                }
            }
            if (!inventory[0].Empty && IsTankOnTop()) // need to call this even if we didn't pump anything this tick
            {
                TryPushIntoTank(); // will try to push whatever it can into a tank...
            }            
        }

        public static WaterTightContainableProps GetWPropsFromPos(IWorldAccessor world, BlockPos pos)
        {
            Block lblock = world.BlockAccessor.GetBlock(pos, BlockLayersAccess.Fluid);
            if (lblock.BlockId != 0 && lblock.IsLiquid())
            {
                ItemStack liqblock = new ItemStack(lblock);
                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(liqblock);
                return props;
            }
            return null;
        }

        public bool IsTankOnTop()
        {
            BlockPos above = Pos.UpCopy(1);
            return Api.World.BlockAccessor.GetBlockEntity(above) is BEFluidTank;
        }

        public void TryPushIntoTank()
        {
            if (inventory[0].Empty) return; // sanity check 1
            WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(inventory[0].Itemstack);
            if (wprops == null) return; // sanity check 2
            BEFluidTank tank = Api.World.BlockAccessor.GetBlockEntity(Pos.UpCopy(1)) as BEFluidTank;
            if (tank == null) return; // sanity check 3
            int amounttomove = 0;
            if (!tank.Inventory[0].Empty) { amounttomove = tank.Inventory[0].MaxSlotStackSize - tank.Inventory[0].Itemstack.StackSize; }
            else amounttomove = tank.Inventory[0].MaxSlotStackSize;
            if (amounttomove == 0) return; // sanity check 4
            
            if (Electric.CurrentPower < ((ulong)_powerPerTankPush)) return; // not enough power to push
            if (inventory[0].Itemstack.StackSize < amounttomove) amounttomove = inventory[0].Itemstack.StackSize;
            ItemStackMoveOperation ismo = new ItemStackMoveOperation(Api.World, EnumMouseButton.Left, (EnumModifierKey)0, EnumMergePriority.AutoMerge, amounttomove);
            int moved = 0;
            moved = (tank.Inventory[0] as ItemSlotLargeLiquid).TryTakeFrom(inventory[0], ref ismo);
            if (moved == 0) return;  
            else 
            {
                Electric.electricpower -= ((ulong)_powerPerTankPush);
                MarkDirty(true); 
            }
        }

        /// <summary>
        /// Checks to ensure this is on top of a valid fluid block.
        /// </summary>
        /// <returns>True if valid</returns>
        public bool CheckForFluid()
        {
            BlockPos below = Pos.DownCopy(1);
            Block blockbelow = Api.World.BlockAccessor.GetBlock(below, BlockLayersAccess.Fluid);
            // TODO check for Blacklist block types

            return blockbelow.Id != 0 && blockbelow.IsLiquid();
        }

        public void GetFluids()
        {
            //_ischeckingfluid = true;
            if (_fluidpositions != null) _fluidpositions.Clear();
            else _fluidpositions = new List<FluidPosition>();

            BlockPos below = this.Pos.DownCopy(1);
            Block blockbelow = Api.World.BlockAccessor.GetBlock(below);
            if (blockbelow.IsLiquid())
            {
                ItemStack liqblock = new ItemStack(blockbelow);
                WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(liqblock);
                if (props != null && props.WhenFilled != null)
                {
                    ItemStack portion = props.WhenFilled.Stack.Resolve(Api.World, "LVPump", true) ? props.WhenFilled.Stack.ResolvedItemstack : null;
                    _fluidtype = blockbelow.LiquidCode;
                    if (portion != null)
                    {
                        // fluid is good, we have liftoff!
                        if (blockbelow.LiquidLevel == 7) // ONLY add full height fluids
                        { 
                            _fluidpositions.Add(new FluidPosition(below, 0)); 
                        }

                        List<BlockPos> _tocheck = new List<BlockPos>();
                        List<BlockPos> _fluidsubs = new List<BlockPos>();
                        _tocheck.Add(below);
                        // TODO Some sort of infinite fluid blacklist support here...
                        while (_tocheck.Count > 0 && _fluidpositions.Count < 10001)
                        {
                            List<BlockPos> _toadd = new List<BlockPos>();

                            foreach (BlockPos bpos in _tocheck)
                            {
                                BlockPos start = bpos.AddCopy(-1, -1, -1);
                                BlockPos end = bpos.AddCopy(1, 1, 1);
                                Api.World.BlockAccessor.WalkBlocks(start, end, delegate (Block dblock, int x, int y, int z)
                                {
                                    // TODO some sort of infinite fluid blacklist check
                                    if (_fluidpositions.Count == 10000) return;

                                    if (dblock.BlockId != 0 && dblock.IsLiquid() && dblock.LiquidCode == _fluidtype)
                                    {
                                        BlockPos bcheck = new BlockPos(x, y, z, 0);
                                        // TODO make pump range a config value check
                                        if (below.ManhattenDistance(bcheck) > 32)  { return; }

                                        FluidPosition bfpos = new FluidPosition(bcheck, below.ManhattenDistance(bcheck));
                                        if (!_fluidpositions.Contains(bfpos))
                                        {
                                            if (dblock.LiquidLevel == 7)
                                            { 
                                                _fluidpositions.Add(bfpos);
                                                _toadd.Add(bcheck);
                                            }
                                            else
                                            {
                                                if (!_fluidsubs.Contains(bcheck))
                                                {
                                                    _fluidsubs.Add(bcheck);
                                                    _toadd.Add(bcheck); // ONLY add to check if we haven't already checked it, prevents infinite loop
                                                }
                                            }                                            
                                        }
                                    }
                                }, false);
                            }
                            _tocheck.Clear();
                            // TODO Some sort of infinite fluid blacklist check
                            if (_toadd.Count > 0 && _fluidpositions.Count < 10001)
                            {
                                _tocheck.AddRange(_toadd);
                            }
                            _toadd.Clear();
                        }
                        _fluidsubs.Clear();
                    }
                }
            }
            if (_fluidpositions.Count >= 10000)
            { 
                _isinfinite = true;
                _fluidpositions.Clear();
            }
            else
            {
                _isinfinite = false;
                _fluidpositions.Sort((x,y) => x.Distance.CompareTo(y.Distance));
            }
            _ischeckingfluid = false;
            MarkDirty(true);
        }

        public void OnSlotModified(int slotid)
        {
            if (_clientupdatedelay > 2)
            {
                _clientupdatedelay = 0;
                MarkDirty(true);
            }
            if (inventory[slotid].Empty) MarkDirty(true);
        }

        public override void CreateBehaviors(Block block, IWorldAccessor worldForResolve)
        {
            base.CreateBehaviors(block, worldForResolve);
            Electric = GetBehavior<ElectricBEBehavior>();
            if (Electric == null)
            {
                worldForResolve.Logger.Fatal("The Electric behavior is required on {0}", Block.Code);
                throw new FormatException("The Electric behavior is required on ${Block.Code}");
            }
        }

        protected virtual void SetState(EnumBEState newstate)
        {
            if (Electric.MachineState != newstate) { Electric.MachineState = newstate; }
            //else return;

            if (Electric.MachineState == EnumBEState.On)
            {
                if (AnimUtil != null && base.Block.Attributes["craftinganimcode"].Exists)
                {
                    AnimUtil.StartAnimation(new AnimationMetaData
                    {
                        Animation = base.Block.Attributes["craftinganimcode"].AsString(),
                        Code = base.Block.Attributes["craftinganimcode"].AsString(),
                        AnimationSpeed = 1f,
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
            MarkDirty(true);
        }

        public override void OnBlockPlaced(ItemStack byItemStack)
        {
            base.OnBlockPlaced(byItemStack);
            // <see cref="BlockEntityContainer.OnBlockPlaced"/> has a bug where it doesn't call the
            // block entity behaviors. So call them here to work around the bug.
            foreach (BlockEntityBehavior behavior in Behaviors)
            {
                behavior.OnBlockPlaced(byItemStack);
            }
        }

        public ItemSlotLiquidOnly GetLiquidAutoPushIntoSlot(BlockFacing blockFacing, ItemSlot fromSlot)
        {
            return null;
        }

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            if (blockFacing != null && blockFacing == BlockFacing.UP) return inventory[0] as ItemSlotLiquidOnly;
            return null;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);
            if (_ischeckingfluid)
            {
                dsc.AppendLine(Lang.Get("vinteng:gui-ischecking"));
            }
            else
            {
                if (_fluidtype != null)
                {
                    dsc.AppendLine($"{Lang.Get("vinteng:gui-word-pumping")} {_fluidtype}");
                    if (_isinfinite)
                    {
                        dsc.AppendLine(Lang.Get("vinteng:gui-isinfinite"));
                    }
                    else dsc.AppendLine($"{_pumpcount} {Lang.Get("vinteng:gui-blocks")} {Lang.Get("vinteng:gui-word-remaining")}");
                }
                else
                {
                    dsc.AppendLine($"{Lang.Get("vinteng:gui-word-not")} {Lang.Get("vinteng:gui-word-pumping")}");
                }
                if (inventory[0].Itemstack != null)
                {
                    WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(inventory[0].Itemstack);
                    float perliter = wprops != null ? wprops.ItemsPerLitre : 100f;
                    dsc.AppendLine($"{inventory[0].Itemstack.StackSize/perliter}L/{inventory[0].MaxSlotStackSize/perliter}L");
                }
            }
            if (Electric.MachineState == EnumBEState.Paused)
            {
                if (Electric.CurrentPower < Electric.MaxPPS)
                {
                    dsc.AppendLine(Lang.Get("vinteng:gui-machine-lowpower"));
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            tree.SetBool("isinfinite", _isinfinite);
            tree.SetString("fluidtype", _fluidtype);
            tree.SetInt("pumpcount", _fluidpositions == null ? 0 : _fluidpositions.Count);
            tree.SetBool("ischeckingfluid", _ischeckingfluid);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) inventory.AfterBlocksLoaded(worldAccessForResolve);
            _isinfinite = tree.GetBool("isinfinite");
            _fluidtype = tree.GetString("fluidtype");
            
            _pumpcount = tree.GetInt("pumpcount");
            _ischeckingfluid = tree.GetBool("ischeckingfluid", false);
            if (Api != null && Api.Side == EnumAppSide.Client) SetState(Electric.MachineState);
        }

        public void DropContents(Vec3d atPos)
        {            
        }
    }
}
