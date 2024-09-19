using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace VintageEngineering.inventory
{
    public class InvCharger : InventoryBase
    {
        ItemSlot _slot;

        public override ItemSlot this[int slotId] { get => _slot; set => _slot = value; }

        public override int Count => 1;

        public InvCharger(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            _slot = new ItemSlot(this);
            _slot.MaxSlotStackSize = 1;
        }

        public override void LateInitialize(string inventoryID, ICoreAPI api)
        {
            base.LateInitialize(inventoryID, api);
        }
        public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            if (_slot.Empty) return _slot;
            else return null;
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            if (!_slot.Empty)
            {
                bool chargable = _slot.Itemstack.Attributes.GetBool("chargable", false);
                IChargeableItem chargeableItem = _slot.Itemstack.Collectible as IChargeableItem;
                if (chargable)
                {
                    int curcharge = _slot.Itemstack.Collectible.GetRemainingDurability(_slot.Itemstack);
                    int maxcharge = _slot.Itemstack.Collectible.Durability;
                    if (curcharge == maxcharge) return _slot;
                }
                else
                {
                    if (chargeableItem.CurrentPower == chargeableItem.MaxPower) return _slot;
                }
            }
            return null;
        }

        public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
        {
            if (sourceSlot.Empty) return false;

            bool chargable = sourceSlot.Itemstack.Collectible.Attributes["chargable"].AsBool(false);
            bool ichargable = sourceSlot.Itemstack.Collectible is IChargeableItem;

            if (chargable || ichargable) return true;
            return false;
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {            
            this._slot = this.SlotsFromTreeAttributes(tree, new ItemSlot[] { this._slot }, null)[0];
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.SlotsToTreeAttributes(new[] { _slot }, tree);
            this.ResolveBlocksOrItems();
        }
    }
}
