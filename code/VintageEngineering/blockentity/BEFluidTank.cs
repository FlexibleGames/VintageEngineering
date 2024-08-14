using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.blockentity
{
    public class BEFluidTank : BlockEntityLiquidContainer, IVELiquidInterface
    {
        private int _capacityLitres;        

        public int CapacityLitres => _capacityLitres;
        
        public override string InventoryClassName => "vefluidtank";

        public BEFluidTank()
        {            
            inventory = new InventoryGeneric(1, null, null, delegate (int id, InventoryGeneric self)
            {
                return new ItemSlotLiquidOnly(self, int.MaxValue);
            });
            inventory.BaseWeight = 1f;
            inventory.SlotModified += OnSlotModified;
        }

        public void SetFluidOnPlace(ItemStack fluid)
        {
            if (inventory != null && inventory[0] != null && fluid != null)
            {
                if (inventory[0].Empty) inventory[0].Itemstack = fluid;
                else inventory[0].Itemstack.SetFrom(fluid);
            }
        }

        private void OnSlotModified(int slot)
        {
            if (Api.Side == EnumAppSide.Server) this.MarkDirty(true);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            //base.GetBlockInfo(forPlayer, dsc);
            
            if (!inventory[0].Empty) 
            {
                WaterTightContainableProps wprops = BlockLiquidContainerBase.GetContainableProps(inventory[0].Itemstack);
                float perliter = wprops != null ? wprops.ItemsPerLitre : 100f;
                dsc.AppendLine($"{inventory[0].Itemstack.GetName()}: {inventory[0].Itemstack.StackSize / perliter}L"); 
            }
        }

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            _capacityLitres = base.Block.Attributes["capacity"].AsInt(1000);

            (inventory[0] as ItemSlotLiquidOnly).CapacityLitres = _capacityLitres;

            // client stuff for rendering content mesh?? Anyone want to tackle that for me?? :)
        }

        protected override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            return GetLiquidAutoPushIntoSlot(atBlockFace, fromSlot);
        }

        #region IVELiquidInterface
        public int[] InputLiquidContainerSlotIDs => new[] { 0 };

        public int[] OutputLiquidContainerSlotIDs => new[] { 0 };

        public bool AllowPipeLiquidTransfer => true;

        public bool AllowHeldLiquidTransfer => true;

        public float TransferSizeLitresPerSecond => 1;

        public ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing)
        {
            foreach (int slot in OutputLiquidContainerSlotIDs)
            {
                if (!Inventory[slot].Empty) return Inventory[slot] as ItemSlotLiquidOnly;
            }
            return null;
        }

        public ItemSlotLiquidOnly GetLiquidAutoPushIntoSlot(BlockFacing blockFacing, ItemSlot fromSlot)
        {
            foreach (int slot in InputLiquidContainerSlotIDs)
            {
                if (Inventory[slot].Empty) continue;
                if (fromSlot.Itemstack.Equals(Api.World, Inventory[slot].Itemstack, GlobalConstants.IgnoredStackAttributes))
                {
                    return Inventory[slot] as ItemSlotLiquidOnly;
                }
            }
            foreach (int slot in InputLiquidContainerSlotIDs)
            {
                if (Inventory[slot].Empty) return Inventory[slot] as ItemSlotLiquidOnly;
            }
            return null;
        }
        #endregion

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
        }
    }
}
