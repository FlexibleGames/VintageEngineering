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
    public class BEMBDistillationExt : VEMBEntityCore, IVELiquidInterface
    {
        // 1 fence - black
        // 2 platform - mid gray
        // 3 ladder - light gray
        // 4 heavyeng - dark purple
        // 5 concrete - cyan
        // 6 lighteng - pink
        // 8 fluid - yellow
        // 9 wood slab - orange
        // 10 power - lime
        // 11 interaction - red
        // 12 item io - green
        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;

        private BlockPos _distillationBase;
        private string _baseCode = string.Empty;

        #region InventoryStuff
        /// <summary>
        /// Slot ID 0 is fluid output
        /// </summary>
        private InventoryGeneric _inventory;
        /// <summary>
        /// Slot ID 0 is fluid output
        /// </summary>
        public override InventoryBase Inventory => _inventory;
        /// <summary>
        /// How full (0-100) is the Output Tank
        /// </summary>
        public int PercentOutputTank
        {
            get
            {
                if (_inventory[0].Empty) return 0;
                else
                {
                    int portions = _inventory[0].Itemstack.StackSize;
                    float cap = (_inventory[0] as ItemSlotLiquidOnly).CapacityLitres * BlockLiquidContainerBase.GetContainableProps(_inventory[0].Itemstack).ItemsPerLitre;
                    float full = portions / cap;
                    return (int)(full * 100);
                }
            }
        }

        public ItemSlotLiquidOnly OutputTank => _inventory[0] as ItemSlotLiquidOnly;

        public bool HasRoomInOutput(int slotid, ItemStack forStack)
        {
            if (slotid != 0) return false;
            if (forStack == null)
            {
                return PercentOutputTank < 100;
            }
            else
            {
                if (_inventory[slotid].Empty) return true;
                if (_inventory[slotid].Itemstack.Collectible.Code == forStack.Collectible.Code)
                {
                    if (_inventory[slotid].Itemstack.StackSize == _inventory[slotid].Itemstack.Collectible.MaxStackSize) return false;
                }
                else return false;
            }
            return true;
        }

        public override string InventoryClassName => "InvDistExt";
        public int[] InputLiquidContainerSlotIDs => null;
        public int[] OutputLiquidContainerSlotIDs => [0];
        public virtual bool AllowPipeLiquidTransfer
        {
            get
            {
                if (Block.Attributes == null) return false;
                return Block.Attributes["allowPipeLiquidTransfer"].AsBool(false);
            }
        }

        public virtual bool AllowHeldLiquidTransfer
        {
            get
            {
                if (Block.Attributes == null) return false;
                return Block.Attributes["allowHeldLiquidTransfer"].AsBool(false);
            }
        }

        public virtual float TransferSizeLitresPerSecond
        {
            get
            {
                if (Block.Attributes == null) return 0f;
                return Block.Attributes["transferLitresPerSecond"].AsFloat(0.01f);
            }
        }

        public ItemSlotLiquidOnly GetLiquidAutoPushIntoSlot(BlockFacing blockFacing, ItemSlot fromSlot = null)
        {
            return null;            
        }

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            string rotside = Block.Variant["side"];

            if (blockFacing == null) return _inventory[0] as ItemSlotLiquidOnly;

            // if this is facing North, the south face is the fluid output
            string right = BlockFacing.FromCode(rotside).GetCW().Code;
            if (blockFacing.Code != right) return null;

            return _inventory[0] as ItemSlotLiquidOnly;
        }

        private void SlotModified(int slotid)
        {
            MarkDirty(true);
        }

        public ItemSlot GetAutoPushIntoSlot(BlockFacing face, ItemSlot fromSlot)
        {
            if (fromSlot.Empty || !fromSlot.Itemstack.Collectible.IsLiquid()) return null;
            return _inventory[0];
        }

        #endregion

        public BEMBDistillationExt()
        {
            _inventory = new InventoryGeneric(1, null, null, delegate (int id, InventoryGeneric self)
            {                
                return new ItemSlotLiquidOnly(self, 50); // we don't need super large capacities here.
            });
            _inventory.SlotModified += SlotModified;
            _inventory.OnGetAutoPushIntoSlot += GetAutoPushIntoSlot;
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _inventory.Pos = Pos;
            _inventory.LateInitialize($"{InventoryClassName}-{Pos.X}/{Pos.Y}/{Pos.Z}", api);
            (_inventory[0] as ItemSlotLiquidOnly).CapacityLitres = Block.Attributes["fluidCapacityLiters"].AsFloat(1f);
            _baseCode = Block.Attributes["baseCode"].AsString(string.Empty);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;                
            }
            else
            {
                capi = api as ICoreClientAPI;
                if (AnimUtil != null)
                {
                    AnimUtil.InitializeAnimator("vembdistext", null, null, new Vec3f(0f, GetRotation(), 0f));
                }
            }
            if (base.Block.Variant["state"] == "built") FindValidateBase();
            if (_distillationBase != null)
            {
                Api.World.BlockAccessor.GetBlockEntity<BEMBDistillationBase>(_distillationBase)?.NewExtensionBuilt(Pos.Copy());
            }
        }

        public bool FindValidateBase()
        {
            if (Api == null) return false;
            if (_distillationBase == null)
            {
                BlockPos us = this.Pos.Copy();
                us.Down();
                while (us.Y > 1)
                {
                    // this needs to ignore any odd blocks as we can't be sure the order these are loaded into the world
                    Block below = Api.World.BlockAccessor.GetBlock(us);
                    string belowcode = below.Code.Path;
                    
                    if (belowcode.Contains("rock") || belowcode.Contains("soil") || belowcode.Contains("glass") || belowcode.Contains("forestfloor") || belowcode.Contains("clay")) break;

                    if (below.Code.Path.Contains(_baseCode)) 
                    { 
                        _distillationBase = us.Copy();
                        return true;
                    }
                    us.Down(1);
                }
            }
            else
            {
                BEMBDistillationBase distbase = Api.World.BlockAccessor.GetBlockEntity<BEMBDistillationBase>(_distillationBase);
                return distbase != null;
            }
            return false;
        }

        public int GetRotation()
        {
            string side = Block.Variant["side"];
            int adjustedIndex = (BlockFacing.FromCode(side)?.HorizontalAngleIndex ?? 1) + 3 & 3;
            return adjustedIndex * 90;
        }
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (_distillationBase != null) // where did the base go?
            {
                BEMBDistillationBase distbase = Api.World.BlockAccessor.GetBlockEntity<BEMBDistillationBase>(_distillationBase);
                distbase?.GetBlockInfo(forPlayer, dsc);
                string outputfluid = _inventory[0].Empty ? Lang.Get("vinteng:gui-word-empty") : _inventory[0].Itemstack.Collectible.GetHeldItemName(_inventory[0].Itemstack);
                dsc.AppendLine($"{Lang.Get("vinteng:gui-word-output")}: {IconHelper.PercentToBar(PercentOutputTank, 10)} {PercentOutputTank:N0}% {outputfluid}");                
            }
            else
            {
                base.GetBlockInfo(forPlayer, dsc);
            }
        }

        public override void ActivateCore(IWorldAccessor world, Caller caller, BlockSelection blockSel, ITreeAttribute activationArgs = null)
        {
            if (IsBuilt) FindValidateBase();
            else
            {
                _distillationBase = null;
                _inventory.DropAll(caller.Player.Entity.Pos.XYZ, 0);
            }
        }

        public override bool OnPlayerRightClick(IPlayer byPlayer, BlockSelection blockSel)
        {
            if (Api.Side == EnumAppSide.Server)
            {
                if (byPlayer != null && !byPlayer.InventoryManager.ActiveHotbarSlot.Empty && Block.Variant["state"] == "built")
                {
                    if (byPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.Collectible?.Tool == EnumTool.Wrench)
                    {
                        if (blockSel != null && !Api.World.Claims.TryAccess(byPlayer, blockSel.Position, EnumBlockAccessFlags.Use))
                        {
                            return false; // only block if we can't interact via permissions with this block
                        }
                        // player is good, Derrick is built, player is holding a wrench
                        BlockFacing control = BlockFacing.FromCode(Block.Variant["side"]).GetCW();
                        if (blockSel.Face == control)
                        {
                            _inventory.DropAll(byPlayer.Entity.Pos.AsBlockPos.ToVec3d());
                        }
                    }
                }
            }
            return true;
        }       

        #region TreeAttributes
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            ITreeAttribute invtree = new TreeAttribute();
            _inventory.ToTreeAttributes(invtree);
            tree["inventory"] = invtree;
            if (_distillationBase != null) tree.SetBlockPos("basecore", _distillationBase); // need to save/sync for clients
        }
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _inventory.FromTreeAttributes(tree.GetTreeAttribute("inventory"));
            _distillationBase = tree.GetBlockPos("basecore", null);
        }
        #endregion
    }
}
