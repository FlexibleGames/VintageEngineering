﻿using System.Collections.Generic;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;


namespace VintageEngineering.Electrical.Systems
{
    /// <summary>
    /// Interface between Electrical Block Entities and the Catenary mod.
    /// </summary>
    public interface IElectricalConnection
    {
        /// <summary>
        /// Connections this Block Entity has.
        /// </summary>
        /// <param name="wirenodeindex">Index of the WireNode of THIS block to pull from.</param>
        /// <returns>WireNode List of connections</returns>
        List<WireNode> GetConnections(int wirenodeindex);

        /// <summary>
        /// Add an ElectricConnection to this block entity at the wire node index.
        /// </summary>
        /// <param name="wirenodeindex">Index of the WireNode</param>
        /// <param name="newconnection">Connection to Add</param>
        void AddConnection(int wirenodeindex, WireNode newconnection);

        /// <summary>
        /// Removes an ElectricConnection from this block entity at the wire node index.
        /// </summary>
        /// <param name="wirenodeindex">Index of the WireNode</param>
        /// <param name="oldconnection">Connection to remove.</param>
        void RemoveConnection(int wirenodeindex, WireNode oldconnection);

        /// <summary>
        /// Returns the number of connections this node carries per given WireNode index.        
        /// </summary>
        /// <param name="wirenodeindex">Index of the WireNode</param>
        /// <returns>Total connections this node has.</returns>
        int NumConnections(int wirenodeindex);

        /// <summary>
        /// Returns the IElectricalBlockEntity for the BlockEntity or one of its behaviors at given position.
        /// </summary>
        /// <param name="blockAccessor">The accessor for the world</param>
        /// <param name="pos">The position of the block</param>
        /// <returns>The interface, or null if the block at that position does not implement it</returns>
        static IElectricalConnection GetAtPos(IBlockAccessor blockAccessor, BlockPos pos)
        {
            BlockEntity entity = blockAccessor.GetBlockEntity(pos);
            if (entity is IElectricalConnection converted) {
                return converted;
            }
            return entity?.GetBehavior<IElectricalConnection>();
        }
    }
}
