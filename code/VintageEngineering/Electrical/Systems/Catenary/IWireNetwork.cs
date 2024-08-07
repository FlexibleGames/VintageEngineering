﻿using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// Interface to impliment a Wire network. Contains NetworkID functions.
    /// <br>Allows a single block to connect to all Wire Function types.</br>
    /// <br>NetworkID's should always be > 0 never 0 or negative.</br>
    /// </summary>
    public interface IWireNetwork
    {
        /// <summary>
        /// Sets the NetworkID of selectionIndex's network
        /// </summary>
        /// <param name="networkID">NetworkID (long)</param>
        /// <param name="selectionIndex">Selection Index (int)</param>
        /// <returns>True if successful</returns>
        bool SetNetworkID(long networkID, int selectionIndex = 0);

        /// <summary>
        /// Gets the NetworkID associated with the selectionIndex node.
        /// <br>Return 0 if selectionIndex is out of bounds.</br>
        /// </summary>
        /// <param name="selectionIndex">Which node to check.</param>
        /// <returns>NetworkID</returns>
        long GetNetworkID(int selectionIndex = 0);

        /// <summary>
        /// Returns a string containing network information to help players lay out their networks.
        /// </summary>
        /// <returns>String</returns>
        string GetNetworkInfo();

        /// <summary>
        /// Returns the IElectricalBlockEntity for the BlockEntity or one of its behaviors at given position.
        /// </summary>
        /// <param name="blockAccessor">The accessor for the world</param>
        /// <param name="pos">The position of the block</param>
        /// <returns>The interface, or null if the block at that position does not implement it</returns>
        static IWireNetwork GetAtPos(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity entity = blockAccessor.GetBlockEntity(pos);
            if (entity is IWireNetwork converted) {
                return converted;
            }
            return entity?.GetBehavior<IWireNetwork>();
        }
    }
}
