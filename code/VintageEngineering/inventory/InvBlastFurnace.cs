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

namespace VintageEngineering.inventory
{
    /// <summary>
    /// SlotIDs : 0-3 = input, 4 = fuel, 5 = output
    /// </summary>
    public class InvBlastFurnace : InventoryBase
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

            CombustibleProperties cprops = sourceSlot.Itemstack.Collectible.CombustibleProps;
            bool isfuel = cprops != null ? cprops.BurnTemperature > 0 : false;

            // fuel slot id is 4
            if (slotid == 4)
            {
                // source is fuel...?
                return isfuel;
            }
            else
            {
                return true;
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
                if (slotId > 5 || slotId < 0) return null;

                return _slots[slotId];
            }
            set
            {
                if (slotId > 5 || slotId < 0) throw new ArgumentOutOfRangeException("slotId");
                if (value == null) throw new ArgumentNullException("value");
                _slots[slotId] = value;
            }
        }

        protected override ItemSlot NewSlot(int i)
        {
            return new ItemSlotSurvival(this);
        }

        /// <summary>
        /// Slot index 0-3 = input, 4 = fuel, 5 = output
        /// </summary>
        /// <param name="inventoryID"></param>
        /// <param name="api"></param>
        public InvBlastFurnace(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            _slots = base.GenEmptySlots(6);
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
            if (fromSlot == null || fromSlot.Empty) return null;

            CombustibleProperties props = fromSlot.Itemstack.Collectible.CombustibleProps;
            bool isfuel = props != null ? props.BurnTemperature > 0 : false;

            if (isfuel && atBlockFace == BlockFacing.UP)
            {
                // fuel slot coming in from the top...
                return _slots[4];
            }

            // check input first
            for (int s = 0; s < 4; s++)
            {
                if (_slots[s].Empty) return _slots[s];
                else
                {
                    if (_slots[s].Itemstack.Collectible.Equals(
                        _slots[s].Itemstack,
                        fromSlot.Itemstack,
                        GlobalConstants.IgnoredStackAttributes))
                    {
                        if (_slots[s].GetRemainingSlotSpace(fromSlot.Itemstack) > 0) return _slots[s];
                    }
                }
            }
            return null;
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            return _slots[5]; // chutes can only pull from item slot
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
