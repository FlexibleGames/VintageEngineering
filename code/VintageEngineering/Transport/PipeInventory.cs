﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.Transport
{
    /// <summary>
    /// PipeInventory for each pipe segement has a total of 2 slots<br/>
    /// One slot for upgrades (slot index 0)<br/>
    /// One slot for filter (slot index 1)
    /// </summary>
    public class PipeInventory : InventoryBase, ISlotProvider
    {
        private ItemSlot[] _slots;

        public int FaceIndex { get; set; }

        public PipeInventory(string inventoryID, int face, ICoreAPI api) : base(inventoryID, api)
        {
            FaceIndex = face;
            _slots = base.GenEmptySlots(3);
            for (int x = 0; x < 2; x++)
            {
                _slots[x].MaxSlotStackSize = 1;
            }
            _slots[2] = new ItemSlotLiquidOnly(this, 50f);
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
                if (slotId >= _slots.Length || slotId < 0) throw new ArgumentOutOfRangeException("slotId");
                _slots[slotId] = value;
            }
        }

        public override ItemSlot GetAutoPushIntoSlot(BlockFacing atBlockFace, ItemSlot fromSlot)
        {
            return null;
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            return null;
        }

        /// <summary>
        /// Can the sinkSlot contain the item is sourceSlot?
        /// </summary>
        /// <param name="sinkSlot"></param>
        /// <param name="sourceSlot"></param>
        /// <returns>True if yes</returns>
        public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
        {
            // Upgrades
            int slotnum = GetSlotId(sinkSlot);
            if (slotnum == 0)
            {
                string sourcecode = sourceSlot.Itemstack?.Collectible?.FirstCodePart();
                return sourcecode == "vepipeupgrade";
            }
            else if (slotnum == 1) // filter
            {
                string sourcecode = sourceSlot.Itemstack?.Collectible?.FirstCodePart();
                return sourcecode == "vepipefilter";
            }
            else
            {
                return sourceSlot.Itemstack.Collectible.IsLiquid();
            }            
        }

        public override bool RemoveOnClose { get { return true; } }

        public ItemSlot[] Slots { get { return _slots; } }

        public override int Count { get { return _slots.Length; } }

        public override void FromTreeAttributes(ITreeAttribute tree)
        {
            this._slots = SlotsFromTreeAttributes(tree, _slots, null);            
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            SlotsToTreeAttributes(_slots, tree);
            if (Api != null) ResolveBlocksOrItems();
        }
    }
}
