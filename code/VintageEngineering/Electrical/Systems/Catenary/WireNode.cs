using ProtoBuf;
using System;
using System.Reflection;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Electrical.Systems.Catenary
{
    /// <summary>
    /// A generic connection node for any type of network.
    /// <br>It is a unique pair of BlockPos and Index</br>
    /// <br>Defined in JSON Attributes/wireNodes</br>
    /// <br>Cube defines the selection box of this Node, rotatable with RotatedCopy.</br>
    /// </summary>
    [ProtoContract()]
    public class WireNode : RotatableCube, IEquatable<WireNode>
    {
        /// <summary>
        /// Block position of this node.
        /// </summary>
        [ProtoMember(1)]
        public BlockPos blockPos;

        /// <summary>
        /// Index of wire node, both array[index] and set in JSON "index" value.
        /// </summary>
        [ProtoMember(2)]
        public int index;

        /// <summary>
        /// Vec3f coords of where the wire connects. It is the center of the selection box.
        /// <br>Calculated when loaded.</br>
        /// </summary>        
        public Vec3f anchorPos;

        /// <summary>
        /// Value set in JSON: Signal, Power, Communication, Other, All
        /// </summary>
        public EnumWireFunction wirefunction;

        /// <summary>
        /// Value set in JSON: Max Connections of this Wire Node anchor point.
        /// </summary>
        public int maxconnections;

        /// <summary>
        /// Network ID associated with this Node, only set via Mod-added networks. Saved in Attributes["wireNodes"][index]["networkid"]
        /// <br>Catenary Mod ignores this value.</br>
        /// </summary>
        public long networkid;

        public WireNode() { }
        public WireNode(BlockPos pos, int ind, int maxcon, EnumWireFunction funct, Vec3f conpoint)
        {
            if (pos == null)
            {
                throw new ArgumentNullException(nameof(pos));
            }
            this.blockPos = pos;
            this.index = ind;
            maxconnections = maxcon;
            networkid = 0;
            wirefunction = funct;
            anchorPos = conpoint;
        }

        public WireNode(JsonObject anchor)
        {
            index = anchor["index"].AsInt(0);

            string wfunct = anchor["wirefunction"].AsString()?.ToLower();
            
            wirefunction = Enum.Parse<EnumWireFunction>(wfunct, true);
            maxconnections = anchor["maxconnections"].AsInt(1);
            networkid = anchor["networkid"].AsObject<long>(0);
            base.Set(anchor["x1"].AsFloat(0.4f),
                    anchor["y1"].AsFloat(0.4f),
                    anchor["z1"].AsFloat(0.4f),
                    anchor["x2"].AsFloat(0.6f),
                    anchor["y2"].AsFloat(0.6f),
                    anchor["z2"].AsFloat(0.6f));
            base.RotateX = anchor["rotateX"].AsFloat(0f);
            base.RotateZ = anchor["rotateZ"].AsFloat(0f);
            base.RotateY = anchor["rotateY"].AsFloat(0f);
            anchorPos = new Vec3f(MidX, MidY, MidZ);
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
