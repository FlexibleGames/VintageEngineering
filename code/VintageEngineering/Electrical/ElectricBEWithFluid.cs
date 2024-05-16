using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.API;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical
{
    public abstract class ElectricBEWithFluid: ElectricBE, IVELiquidInterface
    {
        #region IVELiquidInterface
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
        public virtual int[] InputLiquidContainerSlotIDs => new int[] { 0 };
        public virtual int[] OutputLiquidContainerSlotIDs => new int[] { 0 };

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
    }
}
