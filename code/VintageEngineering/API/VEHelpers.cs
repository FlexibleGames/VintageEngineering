using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;

namespace VintageEngineering.API
{
    public static class VEHelpers
    {
        /// <summary>
        /// A quick check to determine if a chunk at a given position is loaded.<br/>
        /// Unlike the base-game call, this one ignores neighboring chunks.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="atpos">BlockPos to check.</param>
        /// <returns>True if chuck is loaded.</returns>
        public static bool IsChunkLoaded(IWorldAccessor world, BlockPos atpos)
        {
            if (world.BlockAccessor.GetChunk(atpos.X / GlobalConstants.ChunkSize,
                atpos.InternalY / GlobalConstants.ChunkSize,
                atpos.Z / GlobalConstants.ChunkSize) == null)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// Checks if all chunks from given position around at a given radius are loaded.<br/>
        /// If any of them are not loaded it will return false;
        /// </summary>
        /// <param name="world"></param>
        /// <param name="atpos"></param>
        /// <param name="radius"></param>
        /// <returns></returns>
        public static bool IsChunkLoadedRadius(IWorldAccessor world, BlockPos atpos, int radius = 1)
        {
            if (IsChunkLoaded(world, atpos))
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos tocheck = atpos.AddCopy(x * GlobalConstants.ChunkSize, 0, z * GlobalConstants.ChunkSize);
                        if (!IsChunkLoaded(world, tocheck)) return false;
                    }
                }
            }
            else return false;

            return true;
        }
    }
}
