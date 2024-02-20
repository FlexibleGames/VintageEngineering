using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// Object that is used for the 'pending' wire placement
    /// </summary>
    public class WirePlacerWorkSpace
    {
        public BlockPos startPos;
        public Vec3f startOffset;
        public long startNetID;
        public Vec3f endOffset;
        public int startIndex;
        public int endIndex;
        public float thickness;
        public MeshData currentMesh;
        public MeshRef currentMeshRef;
        public bool nowBuilding;
        public EnumWireFunction wireFunction;
        public Block block;
    }
}
