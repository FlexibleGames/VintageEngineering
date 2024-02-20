using System;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// A placed wire connection in the world.
    /// </summary>
    [ProtoContract()]
    public class PlacedWire
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
                this.Block = value;
            }
        }

        /// <summary>
        /// Very specific Vec3f pos for start of wire
        /// </summary>
        [ProtoMember(1)]
        public Vec3f Start;

        /// <summary>
        /// Very specific Vec3f pos for end of wire
        /// </summary>
        [ProtoMember(2)]
        public Vec3f End;

        /// <summary>
        /// Wire Function
        /// </summary>
        [ProtoMember(3)]
        public EnumWireFunction WireFunction;

        /// <summary>
        /// Sets Texture
        /// </summary>
        [ProtoMember(4)]
        public int BlockId;

        /// <summary>
        /// Thickness of the Wire, typically set in JSON.
        /// </summary>
        [ProtoMember(5)]
        public float WireThickness;
        
        /// <summary>
        /// What BlockPos is the owning block for the start position
        /// </summary>
        [ProtoMember(6)]
        public BlockPos MasterStart;

        /// <summary>
        /// What BlockPos is the owning block for the end position
        /// </summary>
        [ProtoMember(7)]
        public BlockPos MasterEnd;

        /// <summary>
        /// Holds the MeshData of the wire, to save re-generating it.
        /// </summary>
        [ProtoMember(8)]
        public MeshData WireMeshData;

        /// <summary>
        /// SelectionBox index of the Starting position
        /// </summary>
        [ProtoMember(9)]
        public int StartSelectionIndex;

        /// <summary>
        /// SelectionBox index of the Ending position
        /// </summary>
        [ProtoMember(10)]
        public int EndSelectionIndex;

        [ProtoMember(11)]
        public long NetworkID;

        private Block block; 

        public PlacedWire() { }

        /// <summary>
        /// Creates a new PlacedWire object.
        /// </summary>
        /// <param name="vstart">Specific Vec3f start point</param>
        /// <param name="vend">Specific Vec3f end point</param>
        /// <param name="wfunction">Wire Function Enum</param>
        /// <param name="blockid">BlockID of wire variant</param>
        /// <param name="wirethickness">Thickness of wire</param>
        /// <param name="start">BlockPos of Master Start block</param>
        /// <param name="end">BlockPos of Master End block</param>
        /// <param name="startindex">[Optional] Index of SelectionBox of vstart node.</param>
        /// <param name="endindex">[Optional] Index of SelectionBox of vend node.</param>
        /// <param name="networkID">[Optional] NetworkID of wire network.</param>
        /// <param name="mesh">[Optional] MeshData of wire, to prevent constant rebuilding of mesh.</param>
        public PlacedWire(Vec3f vstart, Vec3f vend, EnumWireFunction wfunction, 
                          int blockid, float wirethickness,
                          BlockPos start, BlockPos end, 
                          int startindex = 0, int endindex = 0, long networkID = 0L, MeshData mesh = null)
        {
            this.Start = vstart;
            this.End = vend;
            this.WireFunction = wfunction;
            this.BlockId = blockid;
            this.WireThickness = wirethickness;
            this.MasterStart = start;
            this.MasterEnd = end;
            this.StartSelectionIndex = startindex;
            this.EndSelectionIndex = endindex;
            NetworkID = networkID;
            if (mesh != null) WireMeshData = mesh;
        }

        public bool Equals(PlacedWire otherPos)
        {
            if (otherPos is null) return false;
            // Starting and ending points are the same
            bool startmatch = (Start == otherPos.Start && End == otherPos.End);

            // connection exists, just the other direction
            //bool mismatch = (Start == otherPos.End && End == otherPos.Start);

            // ^ = xor (exclusive or) 
            return startmatch;// ^ mismatch;
        }

        public override bool Equals(object obj)
        {
            return Equals(obj as PlacedWire);
        }

        public override int GetHashCode()
        {
            return Start.GetHashCode() * (128 * (int)WireFunction) + End.GetHashCode();
        }

        public static bool operator ==(PlacedWire left, PlacedWire right)
        {
            if (left is null) return right is null;

            return left.Equals(right);
        }

        public static bool operator !=(PlacedWire left, PlacedWire right)
        {
            return !(left == right);
        }
    }
}
