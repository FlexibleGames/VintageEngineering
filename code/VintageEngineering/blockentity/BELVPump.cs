using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using VintageEngineering.Electrical;
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
        private ItemStack _fluidtype;
        private List<FluidPosition> _fluidpositions;
        private bool _ischeckingfluid = true;
        private bool _isinfinite = false;
        private int _pumpcount = 0; // used only on client

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
            
            if (api.Side == EnumAppSide.Server) 
            {
                if (CheckForFluid()) TyronThreadPool.QueueTask(GetFluids, "VELVPump");
            }
        }

        public void OnSimTick(float dt)
        {
            _clientupdatedelay += dt;
            if (_ischeckingfluid || _fluidpositions == null) return;
            if (_clientupdatedelay > 60)
            {
                if (Electric.MachineState != EnumBEState.Sleeping) SetState(EnumBEState.Sleeping);
                _clientupdatedelay = 0;
                return;
            }
            else 
            { 
                if (Electric.MachineState != EnumBEState.On) SetState(EnumBEState.On); 
            }
            if (_fluidpositions.Count == 0 && !_isinfinite) return;

            FluidPosition last = _isinfinite ? new FluidPosition(Pos.DownCopy(1), 0) : _fluidpositions.Last<FluidPosition>();

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
                            // tank has room for another 'block'  
                            Tank.Itemstack.StackSize += (int)portionperblock; // add fluid to tank
                            if (!_isinfinite)
                            {
                                Api.World.BlockAccessor.SetBlock(0, last.Position, BlockLayersAccess.Fluid);
                                Api.World.BlockAccessor.TriggerNeighbourBlockUpdate(last.Position);
                                _fluidpositions.Remove(last);
                            }
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
                    }
                }
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

        /// <summary>
        /// Checks to ensure this is on top of a valid fluid block.
        /// </summary>
        /// <returns>True if valid</returns>
        public bool CheckForFluid()
        {
            BlockPos below = Pos.DownCopy(1);
            Block blockbelow = Api.World.BlockAccessor.GetBlock(below, BlockLayersAccess.Fluid);
            // TODO check for Blacklist block types

            return blockbelow.Id != 0;
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
                    _fluidtype = portion.Clone();
                    if (portion != null)
                    {
                        // fluid is good, we have liftoff!
                        _fluidpositions.Add(new FluidPosition(below, 0));

                        List<BlockPos> _tocheck = new List<BlockPos>();
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
                                    if (dblock.BlockId != 0 && dblock.BlockId == blockbelow.BlockId)
                                    {
                                        BlockPos bcheck = new BlockPos(x, y, z, 0);
                                        // TODO make pump range a config value check
                                        if (below.ManhattenDistance(bcheck) > 32)  { return; }

                                        FluidPosition bfpos = new FluidPosition(bcheck, below.ManhattenDistance(bcheck));
                                        if (!_fluidpositions.Contains(bfpos))
                                        {
                                            _fluidpositions.Add(bfpos);
                                            _toadd.Add(bcheck);
                                            // TODO some sort of infinite fluid blacklist check
                                            if (_fluidpositions.Count == 10000) return;
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
            Electric.MachineState = newstate;

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
                dsc.AppendLine($"Checking Fluid Volume...");
            }
            else
            {
                if (_fluidtype != null)
                {
                    dsc.AppendLine($"{Lang.Get("vinteng:gui-word-pumping")} {_fluidtype.GetName()}");
                    if (_isinfinite)
                    {
                        dsc.AppendLine($"Source is considered infinite.");
                    }
                    else dsc.AppendLine($"{_pumpcount} {Lang.Get("vinteng:gui-blocks")} {Lang.Get("vinteng:gui-word-remaining")}");
                }
                else
                {
                    dsc.AppendLine($"{Lang.Get("vinteng:gui-word-not")} {Lang.Get("vinteng:gui-word-pumping")}");
                }
                if (inventory[0].Itemstack != null)
                {
                    dsc.AppendLine($"{inventory[0].Itemstack.StackSize}/{inventory[0].MaxSlotStackSize}");
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
            tree.SetItemstack("fluidtype", _fluidtype);
            tree.SetInt("pumpcount", _fluidpositions == null ? 0 : _fluidpositions.Count);
            tree.SetBool("ischeckingfluid", _ischeckingfluid);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            if (Api != null) inventory.AfterBlocksLoaded(worldAccessForResolve);
            _isinfinite = tree.GetBool("isinfinite");
            _fluidtype = tree.GetItemstack("fluidtype");
            if (_fluidtype != null) _fluidtype.ResolveBlockOrItem(worldAccessForResolve);
            _pumpcount = tree.GetInt("pumpcount");
            _ischeckingfluid = tree.GetBool("ischeckingfluid", false);
            SetState(Electric.MachineState);
        }

        public void DropContents(Vec3d atPos)
        {            
        }
    }
}
