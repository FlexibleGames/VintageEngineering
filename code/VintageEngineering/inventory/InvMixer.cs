using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering
{
    public class InvMixer: InventoryBase
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        private ItemSlot[] _slots;
        public IPlayer machineuser;

        public override float GetSuitability(ItemSlot sourceSlot, ItemSlot targetSlot, bool isMerge)
        {
            //if (targetSlot == _slots[_slots.Length - 1])
            //{
            //    return 0f;
            //}
            //if (targetSlot == _slots[0])
            //{
            //    return 4f;
            //}
            return base.GetSuitability(sourceSlot, targetSlot, isMerge);
        }

        /// <summary>
        /// Can the sinkSlot contain the item in sourceSlot?
        /// </summary>
        /// <param name="sinkSlot"></param>
        /// <param name="sourceSlot"></param>
        /// <returns>True if yes</returns>
        public override bool CanContain(ItemSlot sinkSlot, ItemSlot sourceSlot)
        {
            int slotid = GetSlotId(sinkSlot);

            if (slotid == -1) return false;

            bool fluid = sourceSlot.Itemstack.Collectible.IsLiquid();
            // liquid slot ids are 4,5, and 7
            if (fluid)
            {
                // source is a fluid
                if (slotid == 4 || slotid == 5 || slotid == 7) return true;
                return false;
            }
            else
            {
                // source is NOT a fluid
                if ((slotid >= 0 && slotid < 4) || slotid == 6) return true;
                return false;
            }
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
                if (slotId > 7 || slotId < 0) return null;

                return _slots[slotId];
            }
            set
            {
                if (slotId > 7 || slotId < 0) throw new ArgumentOutOfRangeException("slotId");
                if (value == null) throw new ArgumentNullException("value");
                _slots[slotId] = value;
            }
        }

        protected override ItemSlot NewSlot(int i)
        {
            return new ItemSlotSurvival(this);
        }

        /// <summary>
        /// Slot index 0-3 item input, 4-5, fluid input, 6 item output, 7 fluid output
        /// </summary>
        /// <param name="inventoryID"></param>
        /// <param name="api"></param>
        public InvMixer(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            _slots = base.GenEmptySlots(8);
            //for (int i = 0; i < 4; i++)
            //{
            //    _slots[i] = new ItemSlotBarrelInput(this);
            //}
            _slots[4] = new ItemSlotLiquidOnly(this, 50); // fluid input 1
            _slots[5] = new ItemSlotLiquidOnly(this, 50); // fluid input 2
            _slots[7] = new ItemSlotLiquidOnly(this, 50); // fluid output             
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
            for (int s = 0; s < 4; s++)
            {
                if (_slots[s].Empty) return _slots[s];
                if (_slots[s].Itemstack.Collectible.Equals(_slots[s].Itemstack, fromSlot.Itemstack, GlobalConstants.IgnoredStackAttributes))
                {
                    if (_slots[s].GetRemainingSlotSpace(fromSlot.Itemstack) > 0) return _slots[s];
                }
            }
            return null;
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {            
            return _slots[6]; // chutes can only pull from item slot, not fluid
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
