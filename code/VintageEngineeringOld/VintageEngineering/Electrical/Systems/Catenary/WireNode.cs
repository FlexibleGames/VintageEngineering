using ProtoBuf;
using System;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// A generic connection node for any type of network.
    /// </summary>
    [ProtoContract()]
    public class WireNode : IEquatable<WireNode>
    {
        /// <summary>
        /// Block position of this node.
        /// </summary>
        [ProtoMember(1)]
        public BlockPos blockPos;

        /// <summary>
        /// Selection Box Index of wire node
        /// </summary>
        [ProtoMember(2)]
        public int index;

        public WireNode() { }
        public WireNode(BlockPos pos, int ind)
        {
            if (pos == null)
            {
                throw new ArgumentNullException(nameof(pos));
            }
            this.blockPos = pos;
            this.index = ind;
        }

        public override int GetHashCode()
        {
            return blockPos.GetHashCode() * 22 + index;
        }

        public override string ToString()
        {
            return $"{blockPos.X}, {blockPos.Y}, {blockPos.Z}: {index}";
        }
        public override bool Equals(object obj)
        {
            WireNode othernode = obj as WireNode;
            return othernode == null ? false : Equals(othernode);
        }
        public bool Equals(WireNode othernode)
        {
            if (othernode == null) return false;
            return (blockPos.Equals(othernode.blockPos) && index == othernode.index);
        }

        public static bool operator ==(WireNode left, WireNode right)
        {
            if (left is null)
            {
                return right is null;
            }
            return left.Equals(right);
        }

        public static bool operator !=(WireNode left, WireNode right)
        {
            return !(left == right);
        }
    }
}
