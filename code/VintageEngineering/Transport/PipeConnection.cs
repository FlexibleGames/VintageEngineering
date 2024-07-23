using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport
{
    /// <summary>
    /// A Pipe Insertion connection.
    /// </summary>
    public class PipeConnection
    {
        private BlockPos pos;
        private BlockFacing facing;
        private int distance;
        //private bool isextraction;
        //private ItemSlot filterslot;

        /// <summary>
        /// BlockPosition of the block connected to.<br/>
        /// NOT the position of the pipe.
        /// </summary>
        public BlockPos Position { get { return pos; } }
        /// <summary>
        /// The pipes Block Face this connection is on (N, E, S, W, U, D)
        /// </summary>
        public BlockFacing Facing { get { return facing; } }
        /// <summary>
        /// Distance TO this connection from a given extraction node.<br/>
        /// Set when building the connection list for a given extraction node.
        /// </summary>
        public int Distance { get { return distance; } }
        /// <summary>
        /// Set a new Distance for this connection
        /// </summary>
        /// <param name="newdist">New distance value.</param>
        public void SetDistance(int newdist) => distance = newdist;

        public PipeConnection(BlockPos bpos, BlockFacing bfacing, int dist = 0)
        {
            pos = bpos;
            facing = bfacing;
            distance = dist;         
        }
        /// <summary>
        /// Create a copy using a new distance value.
        /// </summary>
        /// <param name="newdist">New Distance value</param>
        /// <returns>A copy of this object.</returns>
        public PipeConnection Copy(int newdist)
        {
            PipeConnection acopy = new PipeConnection(this.pos.Copy(), Facing, newdist);
            return acopy;
        }
        /// <summary>
        /// Create an exact copy of this connection.
        /// </summary>
        /// <returns>A copy of this object.</returns>
        public PipeConnection Copy()
        {
            PipeConnection acopy = new PipeConnection(this.pos.Copy(), Facing, Distance);
            return acopy;
        }
    }
}
