using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport
{
    /// <summary>
    /// A Pipe Insertion connection.
    /// </summary>
    [ProtoContract]
    public class PipeConnection: IEquatable<PipeConnection>
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
        //private bool isextraction;
        //private ItemSlot filterslot;


        /// <summary>
        /// BlockPosition of the block connected to.<br/>
        /// NOT the position of the pipe.
        /// </summary>
        public BlockPos Position { get { return _pos; } }
        /// <summary>
        /// The pipes Block Face this connection is on (N, E, S, W, U, D)
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

        public PipeConnection() {}

        public PipeConnection(BlockPos bpos, BlockFacing bfacing, int dist = 0)
        {
            _pos = bpos;
            _facing = bfacing;
            _distance = dist;         
        }
        /// <summary>
        /// Create a copy using a new distance value.
        /// </summary>
        /// <param name="newdist">New Distance value</param>
        /// <returns>A copy of this object.</returns>
        public PipeConnection Copy(int newdist)
        {
            PipeConnection acopy = new PipeConnection(this._pos.Copy(), Facing, newdist);
            return acopy;
        }
        /// <summary>
        /// Create an exact copy of this connection.
        /// </summary>
        /// <returns>A copy of this object.</returns>
        public PipeConnection Copy()
        {
            PipeConnection acopy = new PipeConnection(this._pos.Copy(), Facing, Distance);
            return acopy;
        }

        public virtual void ToTreeAttributes(ITreeAttribute tree)
        {
            tree.SetBlockPos("position", _pos);
            tree.SetString("facing", _facing.Code);
            tree.SetInt("distance", _distance);
        }

        public virtual void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor world)
        {
            _pos = tree.GetBlockPos("position");
            _facing = BlockFacing.FromCode(tree.GetString("facing", "north"));
            _distance = tree.GetInt("distance");
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

        public bool Equals(PipeConnection other)
        {
            return _pos == other._pos && _facing.Code == other.Facing.Code && _distance == other.Distance;
        }
    }
}
