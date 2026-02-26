using System;
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
    /// Slot index 0 = input, 1 = fuel, 2 = item output, 3 = fluid output
    /// </summary>
    public class InvCreosoteOven : InventoryBase
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        /// <summary>
        /// 0 = input, 1 = fuel, 2 = output, 3 = fluid output
        /// </summary>
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

            // liquid slot id is 3
            if (fluid)
            {
                // source is a fluid
                return false;
            }
            else
            {
                // source is NOT a fluid
                if (slotid == 0) return true;
                if (slotid == 1)
                {
                    CombustibleProperties props = sourceSlot.Itemstack.Collectible.CombustibleProps;
                    if (props == null) return false;
                    if (props.BurnTemperature > 0) return true;
                }
                return false;
            }
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
                if (slotId > 3 || slotId < 0) return null;

                return _slots[slotId];
            }
            set
            {
                if (slotId > 3 || slotId < 0) throw new ArgumentOutOfRangeException("slotId");
                if (value == null) throw new ArgumentNullException("value");
                _slots[slotId] = value;
            }
        }

        protected override ItemSlot NewSlot(int i)
        {
            return new ItemSlotSurvival(this);
        }

        /// <summary>
        /// Slot index 0 = input, 1 = fuel, 2 = output, 3 = fluid output
        /// </summary>
        /// <param name="inventoryID"></param>
        /// <param name="api"></param>
        public InvCreosoteOven(string inventoryID, ICoreAPI api) : base(inventoryID, api)
        {
            _slots = base.GenEmptySlots(4);
             _slots[3] = new ItemSlotLiquidOnly(this, 50); // fluid output
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
            if (Pos != null && sapi != null)
            {
                Block block = sapi.World.BlockAccessor.GetBlock(Pos);
                string rotside = block?.Variant["side"];
                BlockFacing parentdir = BlockFacing.FromCode(rotside);

                string atface = atBlockFace.Code;
                bool checkfuel = false;
                if (parentdir.IsAxisNS)
                {
                    if (atBlockFace.IsAxisWE) checkfuel = true;
                }
                else
                {
                    if (atBlockFace.IsAxisNS) checkfuel = true;
                }
                if (atBlockFace == BlockFacing.UP) return _slots[0];
                if (atBlockFace == parentdir.Opposite) return null; // _slots[2];

                if (fromSlot == null || fromSlot.Empty) return null;

                if (checkfuel)
                {
                    CombustibleProperties props = fromSlot.Itemstack.Collectible.CombustibleProps;                    
                    bool isfuel = props != null && props.BurnTemperature > 0;
                    if (isfuel) return _slots[1];
                }
            }
            return null;
        }

        public override ItemSlot GetAutoPullFromSlot(BlockFacing atBlockFace)
        {
            if (Pos != null && sapi != null)
            {
                Block block = sapi.World.BlockAccessor.GetBlock(Pos);
                string rotside = block?.Variant["side"];
                BlockFacing parentdir = BlockFacing.FromCode(rotside);

                if (atBlockFace == parentdir.Opposite) return _slots[2];
            }

            return null;             
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
