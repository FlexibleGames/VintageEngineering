using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering
{
    public class InvLogSplitter : InventoryBase, ISlotProvider
    {
        private ItemSlot[] _slots;

        public InvLogSplitter(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            _slots = base.GenEmptySlots(3); // one input and two outputs            
        }

        public override ItemSlot this[int slotId] 
        { 
            get 
            {
                if (slotId < 0 || slotId >= _slots.Length) return null;
                return _slots[slotId];
            }
            set
            {
                if (slotId > 2 || slotId < 0) throw new ArgumentOutOfRangeException("slotId");
                _slots[slotId] = value;
            }
        }

        public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            return _slots[0];
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            if (_slots[1].Empty) return _slots[2];
            return _slots[1];
        }

        public ItemSlot[] Slots { get { return _slots; } }

        public override int Count {  get { return _slots.Length; } }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            this._slots = SlotsFromTreeAttributes(tree, _slots, null);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(_slots, tree);
            ResolveBlocksOrItems();
        }
    }
}
