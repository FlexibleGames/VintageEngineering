using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Multiblock
{
    /// <summary>
    /// Dummy Block for VE Multiblock System
    /// </summary>
    public class VEMBDummy : Block, IMultiblockOffset
    {
        public BlockPos GetControlBlockPos(BlockPos pos)
        {
            return GetOffset(pos).AsBlockPos;
        }

        /// <summary>
        /// Gets Multiblock offset at given pos<br/>
        /// Returns null if given pos is not a VEMultiblockEntity
        /// </summary>
        /// <param name="pos">BlockPos to check</param>
        /// <returns>Vec3i or Null</returns>
        public Vec3i GetOffset(BlockPos pos)
        {
            return api.World.BlockAccessor.GetBlockEntity<VEMBEntityDummy>(pos)?.Offset;
        }
    }
}
