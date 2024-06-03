using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace VintageEngineering.Transport
{
    [ProtoContract]
    public class PipeNetwork
    {
        [ProtoMember(1)]
        protected long _networkID;
        [ProtoMember(2)]
        protected EnumPipeUse _networkPipeType;
        [ProtoMember(3)]
        protected List<BlockPos> _pipeBlockPositions;

        public long NetworkID
        { get { return _networkID; } set { _networkID = value; } }
        /// <summary>
        /// Pipe type for this network.
        /// </summary>
        public EnumPipeUse NetworkPipeType
        { get => _networkPipeType; }
        /// <summary>
        /// List of Pipe Network Block Positions.
        /// </summary>
        public List<BlockPos> PipeBlockPositions
        { get => _pipeBlockPositions; }

        public PipeNetwork(long networkID, EnumPipeUse pipeType)
        {
            _networkID = networkID;
            _pipeBlockPositions = new List<BlockPos>();
            _networkPipeType = pipeType;
        }

        /// <summary>
        /// Add a Pipe position to this network.
        /// </summary>
        /// <param name="pos">Position to add</param>
        /// <returns>True if successful, false if position already existed.</returns>
        public bool AddPipe(BlockPos pos, IWorldAccessor world)
        {
            if (!_pipeBlockPositions.Contains(pos))
            {
                _pipeBlockPositions.Add(pos);
                BEPipeBase pipe = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
                if (pipe != null) { pipe.NetworkID = _networkID; }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Removes a Pipe position from this network.
        /// </summary>
        /// <param name="pos">Position to remove</param>
        /// <returns>True if successful, false if position wasn't apart of this network.</returns>
        public bool RemovePipe(BlockPos pos, IWorldAccessor world)
        {
            if (_pipeBlockPositions.Contains(pos))
            {
                return _pipeBlockPositions.Remove(pos);                
            }
            return false;
        }

        /// <summary>
        /// Join this network to the given network.
        /// </summary>
        /// <param name="net">Pipe Network to join.</param>
        /// <returns>True if successful, false if a single given position already exists in this network.</returns>
        public bool JoinNetwork(PipeNetwork net, IWorldAccessor world)
        {
            bool duplicatedposition = false;
            foreach (BlockPos pos in net._pipeBlockPositions)
            {
                if (_pipeBlockPositions.Contains(pos)) duplicatedposition = true;
                else AddPipe(pos, world);
            }
            return !duplicatedposition;
        }

        /// <summary>
        /// Validate network, ensuring every position matches networkID, pipe type, and base objects.
        /// </summary>
        /// <param name="bacc">BlockAccessor for world interaction.</param>
        /// <returns>True if all nodes on network are valid.</returns>
        public bool ValidateNetwork(IBlockAccessor bacc)
        {
            bool isValid = true;
            if (NetworkID <= 0) isValid = false;
            foreach (BlockPos pos in _pipeBlockPositions)
            {
                BlockPipeBase pipe = bacc.GetBlock(pos) as BlockPipeBase;
                BEPipeBase pipebe = bacc.GetBlockEntity(pos) as BEPipeBase;
                if (pipe == null || pipebe == null) { isValid = false; break; }
                if (pipe.PipeUse != this._networkPipeType) { isValid = false; break; }
                if (pipebe.NetworkID != this.NetworkID) { isValid = false; }
            }
            return isValid;
        }

        public IEnumerable<BlockPos> GetPipeBlockPositions() {  return _pipeBlockPositions; }

        /// <summary>
        /// Network changed in some way, iterate nodes to update all extraction nodes
        /// </summary>
        /// <param name="world">Required to update blocks and entities.</param>
        public void MarkNetworkDirty(IWorldAccessor world)
        {

        }
    }
}
