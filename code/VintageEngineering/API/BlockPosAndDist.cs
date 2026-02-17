using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.IO;
using System.Runtime.CompilerServices;
using Vintagestory.API.MathTools;

namespace VintageEngineering.API
{
    /// <summary>
    /// A utility class that provides a way of encoding a BlockPos with a distance value.<br/>
    /// Usefull for creating a list of blocks that can be sorted by their distance from some origin.<br/>
    /// Distance set on instantiation or 0 if not provided. <u>Equality ignores BlockPos!</u><br/>
    /// ==, !=, &gt;, and &lt; operators ignore BlockPos and only compare distances
    /// </summary>
    [ProtoContract]
    [JsonObject(MemberSerialization.OptIn)]
    public class BlockPosAndDist : BlockPos, IEquatable<BlockPosAndDist>
    {
        [ProtoMember(1)]
        [JsonProperty]
        public float Distance
        {
            get; set;
        }
        [ProtoMember(2)]
        [JsonProperty]
        public BlockPos Pos { get; set; }
        /// <summary>
        /// Create with Pos, distance defaults to 0
        /// </summary>
        /// <param name="pos">BlockPos of this instance.</param>
        public BlockPosAndDist(BlockPos pos) : base(pos.ToVec3i(), pos.dimension)
        {
            Pos = pos;
            Distance = 0f;
        }
        /// <summary>
        /// Create with pos and distance value.
        /// </summary>
        /// <param name="pos">BlockPos of this instance.</param>
        /// <param name="distance">Float Distance</param>
        public BlockPosAndDist(BlockPos pos, float distance) : base(pos.ToVec3i(), pos.dimension)
        { 
            Pos = pos; 
            Distance = distance; 
        }
        /// <summary>
        /// Create with pos from an Origin, distance calculated by constructor.<br/>
        /// Note: Ensure dimensions are the same!
        /// </summary>
        /// <param name="pos">BlockPos of this instance.</param>
        /// <param name="fromOrigin">BlockPos of origin, distance calculated from this.</param>
        public BlockPosAndDist(BlockPos pos, BlockPos fromOrigin) : base(pos.ToVec3i(), pos.dimension)
        {
            Pos = pos;
            Distance = Pos.DistanceTo(fromOrigin);
        }
        
        public static bool operator ==(BlockPosAndDist lhs, BlockPosAndDist rhs)
        {
            if (lhs == null) return rhs == null;

            return lhs.Equals(rhs); 
        }

        public static bool operator !=(BlockPosAndDist lhs, BlockPosAndDist rhs)
        { 
            return !(lhs == rhs); 
        }
        public static bool operator <(BlockPosAndDist lhs, BlockPosAndDist rhs)
        {
            return lhs.Distance < rhs.Distance;
        }
        public static bool operator >(BlockPosAndDist lhs, BlockPosAndDist rhs)
        {
            return lhs.Distance > rhs.Distance;
        }

        public bool Equals(BlockPosAndDist other)
        {
            if (other == null) return false;
            return (this.Distance == other.Distance);
        }
        public override bool Equals(object obj)
        {
            return Equals(obj as BlockPosAndDist);
        }
        public override int GetHashCode()
        {
            return Pos.GetHashCode() + (int)Distance * 23;
        }
        public new void ToBytes(BinaryWriter writer)
        {
            writer.Write(Pos.X);
            writer.Write(Pos.Y);
            writer.Write(Pos.Z);
            writer.Write(Pos.dimension);
            writer.Write(Distance);
        }
        public new static BlockPosAndDist CreateFromBytes(BinaryReader reader)
        {
            BlockPos newpos = new BlockPos(reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
            return new BlockPosAndDist(newpos, reader.ReadSingle());
        }
        public override string ToString()
        {
            string basestring = base.ToString();
            basestring += $" : {Distance}";
            return basestring;
        }
    }
}
