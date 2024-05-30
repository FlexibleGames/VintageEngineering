using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport
{
    public class PipeExtractionNode : IBlockEntityContainer
    {
        ICoreAPI _api;
        BlockPos _pos;
        protected string faceCode;
        protected PipeInventory inventory;
        protected long listenerID;
        // GUI?

        public ItemSlot Upgrade
        { get { return inventory[0]; } }

        public ItemSlot Filter
        { get { return inventory[1]; } }

        public IInventory Inventory => inventory;

        public string InventoryClassName => $"PipeInventory-{faceCode}";

        public PipeExtractionNode()
        {
            inventory = new PipeInventory(null, null);
            inventory.SlotModified += OnSlotModified;
        }

        public void Initialize(ICoreAPI api, BlockPos pos, string face)
        {
            _api = api;
            _pos = pos;
            faceCode = face;

            inventory.LateInitialize(
                $"{InventoryClassName}/{_pos.X}/{_pos.Y}/{_pos.Z}",
                api
                );            
        }

        /// <summary>
        /// Extraction Node Inventory Slot Modified<br/>
        /// SlotID 0 = PipeUpgrade<br/>
        /// SlotID 1 = PipeFilter
        /// </summary>
        /// <param name="slotid">SlotId modified.</param>
        public virtual void OnSlotModified(int slotid)
        {
            if (slotid == 0)
            {
                // pipe upgrade changed
            }
            else
            {
                // pipe filter changed
            }
        }

        public virtual void OnNodeRemoved()
        {
            inventory.SlotModified -= OnSlotModified;
            DropContents(_pos.ToVec3d());
            if (listenerID != 0)
            {
                BEPipeBase bep = _api.World.BlockAccessor.GetBlockEntity(_pos) as BEPipeBase;
                if (bep != null)
                {
                    bep.RemoveExtractionTickEvent(listenerID);
                }
            }
        }

        /// <summary>
        /// Update Tick for this extraction node.
        /// </summary>
        /// <param name="deltatime">Time (in seconds) since last update.</param>
        public virtual void UpdateTick(float deltatime)
        {

        }

        /// <summary>
        /// Player right clicked this ExtractionNode, passed in from the block.
        /// </summary>
        /// <param name="player">Player who right clicked</param>
        /// <returns>True if event is handled.</returns>
        public virtual bool OnRightClick(IPlayer player)
        {
            // open GUI or auto swap held item in player hotbarslot.
            return true;
        }

        /// <summary>
        /// Drop upgrade and filter for this node.
        /// </summary>
        /// <param name="atPos">Position to drop at.</param>
        public virtual void DropContents(Vec3d atPos)
        {
            inventory.DropAll(atPos);
        }
    }
}
