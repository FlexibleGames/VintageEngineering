using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport
{
    public class PipeConnection
    {
        private BlockPos pos;
        private BlockFacing facing;
        private int distance;

        public BlockPos Position { get { return pos; } }
        public BlockFacing Facing { get { return facing; } }
        public int Distance { get { return distance; } }

        public PipeConnection(BlockPos bpos, BlockFacing bfacing, int dist)
        {
            pos = bpos;
            facing = bfacing;
            distance = dist;
        }
    }
}
