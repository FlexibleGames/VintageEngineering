using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Transport.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Common;

namespace VintageEngineering.Transport.Network
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
        protected bool _isSleeping;

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

        public bool IsSleeping => _isSleeping;

        public void Wake() => _isSleeping = false;


        public PipeNetwork(long networkID, EnumPipeUse pipeType)
        {
            _networkID = networkID;
            _pipeBlockPositions = new List<BlockPos>();
            _networkPipeType = pipeType;
        }

        /// <summary>
        /// Add a Pipe position to this network.<br/>
        /// Sets the network id for the pipes block entity at pos
        /// </summary>
        /// <param name="pos">Position to add</param>
        /// <param name="world">World Accessor</param>
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
        /// Add many pipes to this network from the given list of BlockPos<br/>
        /// If there is a duplicate position, it will be skipped, all positions are processed regardless.<br/>
        /// Sets the networkID of all Pipe entities to this networks ID.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pipes">List of Pipe positions to add.</param>
        /// <returns>True if successful, false if a given position is a duplicate.</returns>
        public bool AddPipes(IWorldAccessor world, List<BlockPos> pipes)
        {
            bool output = true;
            foreach (BlockPos pos in pipes)
            {
                if (_pipeBlockPositions.Contains(pos)) { output = false; }
                else
                {
                    _pipeBlockPositions.Add(pos);
                    BEPipeBase pipe = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
                    if (pipe != null) { pipe.NetworkID = _networkID; }
                }
            }
            return output;
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
        /// Removes many Pipe positions from this network.<br/>
        /// Will process entire list, even if a given pos was not apart of this network.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="remove">List of BlockPos to remove</param>
        /// <returns>True if successful, false if a given position was not apart of this network.</returns>
        public bool RemovePipes(IWorldAccessor world, List<BlockPos> remove)
        {
            bool output = true;
            foreach (BlockPos pos in remove)
            {
                if (!_pipeBlockPositions.Contains(pos)) { output = false; }
                _pipeBlockPositions.Remove(pos);
            }
            return output;
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
                if (pipe.PipeUse != _networkPipeType) { isValid = false; break; }
                if (pipebe.NetworkID != NetworkID) { isValid = false; break; }
            }
            return isValid;
        }

        public IEnumerable<BlockPos> GetPipeBlockPositions() { return _pipeBlockPositions; }

        public void QuickUpdateNetwork(IWorldAccessor world, BlockPos altered, bool isRemove = false)
        {
            foreach (BlockPos pos in _pipeBlockPositions)
            {
                BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
                if (bep == null) continue;
                if (bep.NumExtractionConnections > 0)
                {
                    bep.AlterPushConnections(world, altered, isRemove);
                }
            }
        }

        /// <summary>
        /// Clear this network and any memory it may use.
        /// </summary>
        public void Clear()
        {
            _networkID = 0;
            _pipeBlockPositions.Clear();
        }

        /// <summary>
        /// Network changed in some way, iterate nodes to update all extraction nodes
        /// </summary>
        /// <param name="world">Required to update blocks and entities.</param>
        public void MarkNetworkDirty(IWorldAccessor world)
        {
            List<BlockPos> insertpos = new List<BlockPos>();
            List<BlockPos> extractpos = new List<BlockPos>();

            foreach (BlockPos pos in _pipeBlockPositions)
            {
                if (world.BlockAccessor.GetChunkAtBlockPos(pos) == null) { continue; }
                BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
                if (bep == null) continue;
                if (bep.NumInsertionConnections > 0) insertpos.Add(pos);
                if (bep.NumExtractionConnections > 0) extractpos.Add(pos);
            }
            foreach (BlockPos pos in extractpos)
            {
                BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
                if (bep == null) continue;
                bep.RebuildPushConnections(world, insertpos.ToArray());
            }
        }
    }
}
