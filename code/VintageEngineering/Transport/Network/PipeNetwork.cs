﻿using ProtoBuf;
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

        /// <summary>
        /// Empty constructor for ProtoBuf system
        /// </summary>
        public PipeNetwork() { } 
        /// <summary>
        /// Create a new empty network with the given use and ID.
        /// </summary>
        /// <param name="networkID">ID of this new network</param>
        /// <param name="pipeType">Type of network</param>
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
                if (pipe != null) 
                { 
                    pipe.NetworkID = _networkID;
                    pipe.MarkDirty(true);
                }
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
                    if (pipe != null) 
                    { 
                        pipe.NetworkID = _networkID; 
                        pipe.MarkDirty(true);
                    }
                }
            }
            return output;
        }

        /// <summary>
        /// Removes a Pipe position from this network.<br/>
        /// Does NOT reset the NetworkID of the pipe at the given BlockPos
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
        /// Will process entire list, even if a given pos was not apart of this network.<br/>
        /// Does NOT reset the NetworkID of the pipe at the given BlockPositions
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
        /// Validate network, ensuring every position matches networkID, pipe type, and base objects.
        /// </summary>
        /// <param name="bacc">BlockAccessor for world interaction.</param>
        /// <returns>True if all nodes on network are valid.</returns>
        public bool ValidateNetwork(IBlockAccessor bacc)
        {
            bool isValid = true;
            if (NetworkID <= 0) isValid = false;
            if (_pipeBlockPositions == null || _pipeBlockPositions.Count == 0) return false;
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

        /// <summary>
        /// A fast(er) way to inform a pipe network of a pipe block change on the network<br/>
        /// I.E. A pipe block is added or removed.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="altered">Pipe Block Position altered</param>
        /// <param name="isRemove">True if block removed.</param>
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
        /// A fast(er) way to inform a pipe network of a single PipeConnection change.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="con">PipeConnection</param>
        /// <param name="isRemove">True to remove the PipeConnection, false to add it.</param>
        public void QuickUpdateNetwork(IWorldAccessor world, PipeConnection con, bool isRemove = false)
        {
            PipeConnection[] cons = new PipeConnection[1] { con };
            QuickUpdateNetwork(world, cons, isRemove);
        }
        /// <summary>
        /// A fast(er) way to inform a pipe network of multiple PipeConnection changes.<br/>
        /// Cannot both add and remove connections in the same call, all must be one or the other.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="cons">PipeConnection array of changes</param>
        /// <param name="isRemove">If True these connections will be removed from the list.</param>
        public void QuickUpdateNetwork(IWorldAccessor world, PipeConnection[] cons, bool isRemove = false)
        {
            foreach (BlockPos pos in _pipeBlockPositions)
            {
                BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
                if (bep == null) continue;
                bep.AlterPushConnections(world, cons, isRemove);
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
            if (_pipeBlockPositions == null || _pipeBlockPositions.Count == 0) return;
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
