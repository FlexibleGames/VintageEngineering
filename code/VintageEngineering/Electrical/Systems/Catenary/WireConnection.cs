using System;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// A generic placed wire connection in the world.
    /// <br>Equality is based on StartNode and EndNode BlockPos & Index.</br>
    /// <br>Only ONE wire connection allowed between a set block position and WireNode index.</br>
    /// </summary>
    [ProtoContract()]
    public class WireConnection: IEquatable<WireConnection>
    {
        /// <summary>
        /// The Block that represents this wire, Sets texture and attributes.
        /// </summary>
        public Block Block
        {
            get
            {
                return this.block;
            }
            set
            {
                this.block = value;
                JsonObject attributes = this.block.Attributes;
                this.SlumpPerMeter = ((attributes != null) ? attributes["slumpPerMeter"].AsFloat(0.25f) : 0);
            }
        }

        /// <summary>
        /// Exact Vec3f point of wire start connection anchor.
        /// </summary>
        [ProtoMember(1)]
        public Vec3f VecStart;

        /// <summary>
        /// Exact Vec3f point of wire end connection anchor.
        /// </summary>
        [ProtoMember(2)]
        public Vec3f VecEnd;

        /// <summary>
        /// What WireNode is the owning block for the start position
        /// </summary>
        [ProtoMember(3)]
        public WireNode NodeStart;

        /// <summary>
        /// What WireNode is the owning block for the end position
        /// </summary>
        [ProtoMember(4)]
        public WireNode NodeEnd;

        /// <summary>
        /// Sets Texture, what BlockWire variant is this wire?
        /// </summary>
        [ProtoMember(5)]
        public int BlockId;

        /// <summary>
        /// Thickness of the Wire, pulled from BlockWire JSON.
        /// </summary>
        [ProtoMember(6)]
        public float WireThickness;

        /// <summary>
        /// Holds the MeshData of the wire, saved once generated to save re-generating it Per game session.
        /// <br>Not saved to disk. So must be regenerated on each new game session.</br>
        /// </summary>        
        public MeshData WireMeshData;

        /// <summary>
        /// Holds the MeshRef to the MeshData on the graphics card. Can be used to render it.
        /// <br>Not saved to disk.</br>
        /// </summary>
        public MeshRef WireMeshRef;

        private Block block;

        public float SlumpPerMeter;

        /// <summary>
        /// What chunk is this wire in, starting wires only.
        /// </summary>
        public Vec3i ChunkPos
        {
            get
            {
                Vec3i output = new Vec3i(NodeStart.blockPos.X / 32, NodeStart.blockPos.InternalY / 32, NodeStart.blockPos.Z / 32);
                return output;
            }
        }

        public WireConnection() { }

        /// <summary>
        /// Creates a new WireConnection object.
        /// </summary>
        /// <param name="vstart">Specific Vec3f start point</param>
        /// <param name="vend">Specific Vec3f end point</param>        
        /// <param name="blockid">BlockID of wire variant</param>
        /// <param name="wirethickness">Thickness of wire</param>
        /// <param name="start">WireNode of Master Start block (BlockPos + Index)</param>
        /// <param name="end">WireNode of Master End block (BlockPos + Index)</param>
        /// <param name="wireBlock">BlockWire of the connection</param>
        /// <param name="startindex">[Optional] Index of SelectionBox of vstart node.</param>
        /// <param name="endindex">[Optional] Index of SelectionBox of vend node.</param>        
        /// <param name="mesh">[Optional] MeshData of wire, to prevent constant rebuilding of mesh.</param>
        public WireConnection(Vec3f vstart, Vec3f vend, int blockid, float wirethickness,
                          WireNode start, WireNode end, Block wireBlock,
                          MeshData mesh = null)
        {            
            this.VecStart = vstart;
            this.VecEnd = vend;
            this.BlockId = blockid;
            this.WireThickness = wirethickness;
            this.NodeStart = start;
            this.NodeEnd = end;
            this.block = wireBlock;
            if (mesh != null) WireMeshData = mesh;
        }

        public WireConnection Clone()
        {
            WireConnection output = new WireConnection(VecStart.Clone(), VecEnd.Clone(), BlockId, WireThickness, NodeStart, NodeEnd, block, (WireMeshData != null) ? WireMeshData.Clone() : null);
            return output;
        }

        public bool Equals(WireConnection otherPos)
        {
            if (otherPos is null) return false;
            // Starting and ending points are the same
            bool startmatch = (NodeStart == otherPos.NodeStart && NodeEnd == otherPos.NodeEnd);

            // connection exists, just the other direction
            //bool mismatch = (Start == otherPos.End && End == otherPos.Start);

            // ^ = xor (exclusive or) 
            return startmatch;// ^ mismatch;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as WireConnection);
        }

        public override int GetHashCode()
        {
            return NodeStart.blockPos.GetHashCode() + (8 * NodeStart.index) + NodeEnd.blockPos.GetHashCode() + (8 * NodeEnd.index);
        }

        public static bool operator ==(WireConnection left, WireConnection right)
        {
            if (left is null) return right is null;

            return left.Equals(right);
        }

        public static bool operator !=(WireConnection left, WireConnection right)
        {
            return !(left == right);
        }
    }
}
