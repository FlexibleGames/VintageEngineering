using System;
using System.Collections.Generic;
using Vintagestory.API;
using Vintagestory.Common;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.API.Client;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering
{
    public class TestMachineInventory : InventoryBase, ISlotProvider
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        private ItemSlot[] _slots;
        public IPlayer machineuser;

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            
            if (targetSlot == _slots[0] && sourceSlot.Itemstack.Collectible.FirstCodePart() == "ingot")
            {
                return 4f;
            }
            return base.GetSuitability(sourceSlot, targetSlot, isMerge);
        }


        public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
        {
            return sinkSlot == _slots[0] && sourceSlot.Itemstack.Item.FirstCodePart() == "ingot";
        }

        public override bool HasOpened(IPlayer player)
        {
            return (machineuser != null && machineuser.PlayerUID == player.PlayerUID);
        }

        public override bool RemoveOnClose { get { return true; } }

        public ItemSlot[] Slots
        {
            get { return this._slots; }
        }

        public override int Count
        {
            get { return _slots.Length; }
        }

        public override ItemSlot this[int slotId]
        {
            get
            {
                if (slotId > 1 || slotId < 0) return null;

                return _slots[slotId];
            }
            set
            {
                if (slotId > 1 || slotId < 0) throw new ArgumentOutOfRangeException("slotId");
                if (value == null) throw new ArgumentNullException("value");
                _slots[slotId] = value;
            }
        }

        protected override ItemSlot NewSlot(int i)
        {
            return new ItemSlotSurvival(this);
        }

        public TestMachineInventory(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            _slots = base.GenEmptySlots(2);
        }

        public override void LateInitialize(string inventoryID, ICoreAPI api)
        {
            base.LateInitialize(inventoryID, api);
            if (api.Side == EnumAppSide.Server)
            {
                sapi = api as ICoreServerAPI;
            }
            else
            {
                capi = api as ICoreClientAPI;
            }

        }

        public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            return _slots[0];
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            return _slots[1];
        }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            this._slots = this.SlotsFromTreeAttributes(tree, this._slots, null);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.SlotsToTreeAttributes(_slots, tree);
            this.ResolveBlocksOrItems();
        }
    }
}
