using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace VintageEngineering.API
{
    public interface IVELiquidInterface
    {
        /// <summary>
        /// The set of slotIDs that point to the fluid input tanks.
        /// </summary>
        int[] InputLiquidContainerSlotIDs { get; }

        /// <summary>
        /// The set of slotIDs that point to the fluid output tanks.
        /// </summary>
        int[] OutputLiquidContainerSlotIDs { get; }

        /// <summary>
        /// Does this support Pipe Liquid transfer?
        /// </summary>
        bool AllowPipeLiquidTransfer { get; }
        /// <summary>
        /// Does this support hand-filling tanks?<br/>
        /// When manually moving liquid, it's up to the block to manage volume moved at a time.
        /// </summary>
        bool AllowHeldLiquidTransfer { get; }
        /// <summary>
        /// Transfer liquid amount per second when automated with pipes.
        /// </summary>
        float TransferSizeLitresPerSecond { get; }

        /// <summary>
        /// Returns ItemSlot for pushing liquid into.<br/>
        /// blockFacing and fromSlot can be null.
        /// </summary>
        /// <param name="blockFacing">Block Facing</param>
        /// <param name="fromSlot">Slot containing source liquid.</param>
        /// <returns>ItemSlot for pushing into.</returns>
        ItemSlotLiquidOnly GetLiquidAutoPushIntoSlot(BlockFacing blockFacing, ItemSlot fromSlot = null);

        /// <summary>
        /// Returns ItemSlot for pulling liquids OUT.<br/>
        /// blockFacing can be null
        /// </summary>
        /// <param name="blockFacing">Block Facing</param>
        /// <returns>ItemSlot to pull from.</returns>
        ItemSlotLiquidOnly GetLiquidAutoPullFromSlot(BlockFacing blockFacing);
    }
}
