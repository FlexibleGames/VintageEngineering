using ProtoBuf;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport
{
    /// <summary>
    /// A Pipe Insertion connection.
    /// </summary>
    [ProtoContract]
    public class PipeInsertNode : IEquatable<PipeInsertNode>
    {
        [ProtoMember(1)]
        private BlockPos _pos;
        private BlockFacing _facing;
        [ProtoMember(2)]
        public string FaceCon
        {
            get { return _facing.Code; }
            set { _facing = BlockFacing.FromCode(value); }
        }

        [ProtoMember(3)]
        private int _distance;

        [ProtoMember(4)]
        private string _subNet;
        /// <summary>
        /// A special tag for this Insert Node that only receives items from Extraction Nodes of the same SubNet
        /// </summary>
        public string SubNet
        {
            get => _subNet;
            set { _subNet = value; }
        }

        /// <summary>
        /// BlockPosition of the block connected to.<br/>
        /// NOT the position of the pipe.
        /// </summary>        
        public BlockPos Position { get { return _pos; } }
        /// <summary>
        /// BlockPosition of the PipeBlock that contains this node.
        /// </summary>
        public BlockPos NodePosition
        {
            get
            {
                return Position.AddCopy(Facing.Opposite);
            }
        }
        /// <summary>
        /// The Pipe Block Face this connection is on (N, E, S, W, U, D)
        /// </summary>
        public BlockFacing Facing { get { return _facing; } }
        /// <summary>
        /// Distance TO this connection from a given extraction node.<br/>
        /// Set when building the connection list for a given extraction node.
        /// </summary>
        public int Distance { get { return _distance; } }
        /// <summary>
        /// Set a new Distance for this connection
        /// </summary>
        /// <param name="newdist">New distance value.</param>
        public void SetDistance(int newdist) => _distance = newdist;

        public PipeInsertNode() { }

        /// <summary>
        /// A PipeInsertNode object
        /// </summary>
        /// <param name="bpos">BlockPos of block this Connection is TO, NOT the position of the pipe itself.</param>
        /// <param name="bfacing">BlockFacing TOWARD the block connected to.</param>
        /// <param name="dist">Distance TO this connection, as calculated by the Extraction nodes that contain this connection.</param>
        public PipeInsertNode(BlockPos bpos, BlockFacing bfacing, string subnet, int dist = 0)
        {
            _pos = bpos;
            _facing = bfacing;
            _distance = dist;
            _subNet = subnet;
        }
        /// <summary>
        /// Create a copy using a new distance value.
        /// </summary>
        /// <param name="newdist">New Distance value</param>
        /// <returns>A copy of this object.</returns>
        public PipeInsertNode Copy(int newdist)
        {
            PipeInsertNode acopy = new PipeInsertNode(this._pos.Copy(), Facing, SubNet, newdist);
            return acopy;
        }
        /// <summary>
        /// Create an exact copy of this connection.
        /// </summary>
        /// <returns>A copy of this object.</returns>
        public PipeInsertNode Copy()
        {
            PipeInsertNode acopy = new PipeInsertNode(this._pos.Copy(), Facing, SubNet, Distance);
            return acopy;
        }

        public virtual void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetBlockPos("position", _pos);
            tree.SetString("facing", _facing.Code);
            tree.SetInt("distance", _distance);
            tree.SetString("subnet", _subNet);
        }

        public virtual void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
        {
            _pos = tree.GetBlockPos("position");
            _facing = BlockFacing.FromCode(tree.GetString("facing", "north"));
            _distance = tree.GetInt("distance");
            _subNet = tree.GetString("subnet", string.Empty);
        }

        public byte[] ToBytes()
        {
            TreeAttribute contree = new TreeAttribute();
            ToTreeAttributes(contree);
            return contree.ToBytes();
        }

        public void FromBytes(byte[] bytes)
        {
            TreeAttribute contree = TreeAttribute.CreateFromBytes(bytes);
            FromTreeAttributes(contree, null);
        }

        public bool Equals(PipeInsertNode other)
        {
            return _pos == other._pos && _facing.Code == other.Facing.Code && _subNet == other.SubNet && _distance == other.Distance;
        }
    }
}

