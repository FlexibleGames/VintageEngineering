using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Transport.API;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VintageEngineering.Transport.Network
{
    public class PipeNetworkManager : ModSystem
    {
        private ICoreAPI _api;
        private ICoreServerAPI _sapi;

        private long _nextNetworkID = 1;
        protected Dictionary<long, PipeNetwork> _pipeNetworks;

        public PipeNetwork GetNetwork(long netid)
        {
            if (_pipeNetworks != null && _pipeNetworks[netid] != null) return _pipeNetworks[netid];
            return null;
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            _api = api;
            _sapi = api;
            _sapi.Event.SaveGameLoaded += OnSaveGameLoaded;
            _sapi.Event.GameWorldSave += OnGameSave;
        }

        private void OnGameSave()
        {
            _sapi.WorldManager.SaveGame.StoreData("vepipenetworks", NetworkBytes());
            _sapi.WorldManager.SaveGame.StoreData("vepipenetworknextid", SerializerUtil.Serialize(_nextNetworkID));
        }

        private void OnSaveGameLoaded()
        {
            byte[] networkbytes = _sapi.WorldManager.SaveGame.GetData("vepipenetworks");
            byte[] nextidbytes = _sapi.WorldManager.SaveGame.GetData("vepipenetworknextid");                        
            InitializeNetworkManager(networkbytes, nextidbytes);
        }

        /// <summary>
        /// Next Pipe Network ID that will be used, updated internally, do not override.
        /// </summary>
        public long NextNetworkID
        { get { return _nextNetworkID; } }

        public byte[] NetworkBytes()
        {
            if (_pipeNetworks.Count > 0)
            {
                foreach (KeyValuePair<long, PipeNetwork> net in _pipeNetworks)
                {
                    net.Value.NetworkID = net.Key; // ensure the ID is set for all the networks.
                }
                return SerializerUtil.Serialize(_pipeNetworks);
            }
            return new byte[1];
        }

        public void InitializeNetworkManager(byte[] networks, byte[] nextid)
        {
            if (networks != null && networks.Length > 1)
            { 
                _pipeNetworks = SerializerUtil.Deserialize<Dictionary<long, PipeNetwork>>(networks); 
            }
            else
            {
                _pipeNetworks = new Dictionary<long, PipeNetwork>();
            }
            if (nextid != null)
            { 
                _nextNetworkID = SerializerUtil.Deserialize<long>(nextid); 
            }
        }
        /// <summary>
        /// Creates a network and returns the ID of that network.
        /// </summary>
        /// <param name="pipeuse">The type of pipe of this network.</param>
        /// <returns>long NetworkID of the new network</returns>
        public long CreateNetwork(EnumPipeUse pipeuse)
        {
            long output = NextNetworkID;

            // a fancy null status check and assignment if true.
            _pipeNetworks ??= new Dictionary<long, PipeNetwork>();

            _pipeNetworks.Add(_nextNetworkID, new PipeNetwork(_nextNetworkID, pipeuse));
            _nextNetworkID++;
            return output;
        }
        /// <summary>
        /// Called when a new pipe block is placed but after it updates its own connections.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pos">Position of new pipe block</param>
        public void OnPipeBlockPlaced(IWorldAccessor world, BlockPos pos)
        {
            // check the sides for other pipes and check connection overrides
            // compare all networkID's
            // Merge networks if needed create network if needed, set pos networkid
            BEPipeBase us = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;            
            BlockPipeBase usb = world.BlockAccessor.GetBlock(pos) as BlockPipeBase;
            if (us == null || usb == null) return;
            int pipecons = 0;
            bool hasinserts = false;

            for (int f = 0; f < 6; f++)
            {
                if (us.ConnectionSides[f])
                {
                    BEPipeBase them = world.BlockAccessor.GetBlockEntity(pos.AddCopy(BEPipeBase.ConvertIndexToFace(f))) as BEPipeBase;
                    if (them == null) continue;
                    pipecons++;
                    if (us.NetworkID == 0)
                    {
                        us.NetworkID = them.NetworkID;
                        _pipeNetworks[us.NetworkID].AddPipe(pos.Copy(), world);
                    }
                    else
                    {
                        if (us.NetworkID != them.NetworkID)
                        {
                            MergeNetworks(world, us.NetworkID, them.NetworkID);
                            hasinserts = true;
                        }
                    }
                }
                if (us.InsertionSides[f]) hasinserts = true;
            }
            if (pipecons == 0)
            {
                long newid = CreateNetwork(usb.PipeUse);                    
                _pipeNetworks[newid].AddPipe(pos.Copy(), world);
                _pipeNetworks[newid].MarkNetworkDirty(world); // rebuilds insert list for all extraction nodes
            }
            if (pipecons == 1 && hasinserts)
            {
                _pipeNetworks[us.NetworkID].QuickUpdateNetwork(world, pos.Copy(), false);
            }
            if (pipecons > 1 && hasinserts)
            {
                // if we have an insert node and have merged networks, we need to inform the rest 
                // of the network to rebuild their pushConnection lists.
                _pipeNetworks[us.NetworkID].MarkNetworkDirty(world);
            }
        }

        public void OnPipeBlockBroken(IWorldAccessor world, BlockPos pos)
        {
            // check connection sides for pipe connections
            // if endpoint, remove node; otherwise split network
            BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
            int pipecons = 0;
            int pipeinserts = bep.NumInsertionConnections;
            long netid = bep.NetworkID;

            if (bep == null) return;
            for (int f = 0; f < 6; f++)
            {
                if (bep.ConnectionSides[f]) pipecons++;                
            }
            if (pipecons == 0)
            {
                _pipeNetworks.Remove(netid);
            }
            else if (pipecons == 1)
            {
                _pipeNetworks[netid].RemovePipe(pos, world);
                if (pipeinserts > 0)
                {
                    _pipeNetworks[netid].QuickUpdateNetwork(world, pos, true);
                }
            }
            else
            {
                SplitNetworkAt(world, pos);
            }
        }

        /// <summary>
        /// Called when the player force-removes a pipe-pipe connection
        /// potentially splitting the network.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pos">Position of the override.</param>
        /// <param name="selection">Face selection of the override.</param>
        /// <param name="overrideState">True if connection node is overridden.</param>
        public void OnPipeConnectionOverride(IWorldAccessor world, BlockPos pos, BlockSelection selection, bool overrideState)
        {
            // this process should be much easier as there's just an A and B side to track...
            BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
            BlockPipeBase pblock = world.BlockAccessor.GetBlock(pos) as BlockPipeBase;
            if (bep == null ||  pblock == null) return; // sanity check

            BEPipeBase other = world.BlockAccessor.GetBlockEntity(pos.AddCopy(BEPipeBase.ConvertIndexToFace(selection.SelectionBoxIndex))) as BEPipeBase;


            if (overrideState)
            {
                // we are forcefully disconnecting the pipe-pipe connection
                List<BlockPos> othernet = GetConnectedPipes(world, other.Pos);

                if (othernet.Contains(pos)) { return; } // the network is still connected elsewhere... do not split
                else
                {
                    long newid = CreateNetwork(pblock.PipeUse);
                    _pipeNetworks[newid].AddPipes(world, othernet);
                    _pipeNetworks[bep.NetworkID].RemovePipes(world, othernet);
                }
            }
            else
            {
                // the connection override has been removed, the networks should merge
                MergeNetworks(world, bep.NetworkID, other.NetworkID);
            }
        }
        /// <summary>
        /// Merge Pipe Network net2 into net1.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="net1id">PipeNetwork to grow</param>
        /// <param name="net2id">PipeNetwork to merge into net1</param>
        public void MergeNetworks(IWorldAccessor world, long net1id, long net2id)
        {
            // Sanity check
            if (!_pipeNetworks.ContainsKey(net2id) || !_pipeNetworks.ContainsKey(net1id)) return;
            
            foreach (BlockPos pos in _pipeNetworks[net2id].PipeBlockPositions)
            {
                // this also sets the Pipes NetworkID of the block entity
                _pipeNetworks[net1id].AddPipe(pos.Copy(), world);
            }
            _pipeNetworks[net1id].MarkNetworkDirty(world); // rebuilds insert list for all extraction nodes
            _pipeNetworks[net2id].Clear();
            _pipeNetworks.Remove(net2id);
        }
        /// <summary>
        /// Splits a network at the given BlockPos
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pos">Pipe Block being removed.</param>
        public void SplitNetworkAt(IWorldAccessor world, BlockPos pos)
        {
            // check pipe connections for neighboring pipes, key = face
            List<BlockPos> connectedpipes = new List<BlockPos>();

            BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
            BlockPipeBase pipeblock = world.BlockAccessor.GetBlock(pos) as BlockPipeBase;
            if (bep == null || pipeblock == null) return; // something is wrong

            long splitid = bep.NetworkID; // the networkID of the source of the split

            for (int f = 0; f < 6; f++)
            {
                if (bep.ConnectionSides[f])
                {
                    connectedpipes.Add(pos.AddCopy(BEPipeBase.ConvertIndexToFace(f)));
                    bep.OverridePipeConnectionFace(f, true); // disconnect the face to avoid false connections.
                }
            }
            // we need to remove THIS pipe from its network
            _pipeNetworks[splitid].RemovePipe(pos, world);

            // connectedpipes now has a list of all the pipe block positions of new (potential) networks.
            // the connections of the position passed in have also been disabled to prevent false connections.
            if (connectedpipes.Count > 1)
            {
                BlockPos firstpos = connectedpipes[0];
                List<BlockPos> firstcon = GetConnectedPipes(world, firstpos.Copy(), pos.Copy());
                connectedpipes.Remove(firstpos);

                for (int x = 0; x < connectedpipes.Count; x++)
                {
                    long conid = (world.BlockAccessor.GetBlockEntity(connectedpipes[x]) as BEPipeBase).NetworkID;
                    if (firstcon.Contains(connectedpipes[x]) || splitid != conid)
                    {
                        // When splitid != conid that means we've already processed that list of pipes into a new network
                        // the next connection is part of the first, skip
                        continue;
                    }
                    else
                    {
                        // this is a new network
                        List<BlockPos> newnet = GetConnectedPipes(world, connectedpipes[x].Copy(), pos.Copy());
                        long newid = CreateNetwork(pipeblock.PipeUse);
                        // add the pipes to the new network
                        _pipeNetworks[newid].AddPipes(world, newnet);
                        _pipeNetworks[newid].MarkNetworkDirty(world); // rebuilds insert list for all extraction nodes
                        // remove those same pipes from the original network
                        _pipeNetworks[splitid].RemovePipes(world, newnet);
                    }
                }
            }
        }

        /// <summary>
        /// Returns List of BlockPos of all the pipes connected to the given pos.<br/>
        /// By far the most expensive call in this feature, optimizations to come later.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pos">Start Position to check from.</param>
        /// <param name="skippos">BlockPos to skip and ignore all connections to/from.</param>
        /// <returns>List of BlockPos of all connected pipes.</returns>
        public List<BlockPos> GetConnectedPipes(IWorldAccessor world, BlockPos pos, BlockPos skippos = null)
        {
            List<BlockPos> connectedpipes = new List<BlockPos>();
            List<BlockPos> pipestoprocess = new List<BlockPos>();

            BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;

            connectedpipes.Add(pos);
            pipestoprocess.AddRange(bep.GetPipeConnections(skippos));

            while (pipestoprocess.Count > 0)
            {
                List<BlockPos> nodestoadd = new List<BlockPos>();
                foreach (BlockPos node in pipestoprocess)
                {
                    if (!connectedpipes.Contains(node))
                    {
                        connectedpipes.Add(node);
                    }
                    else continue;

                    BEPipeBase pipe = world.BlockAccessor.GetBlockEntity(node) as BEPipeBase;
                    if (pipe == null) continue; // sanity check
                    nodestoadd.AddRange(pipe.GetPipeConnections(skippos));
                }
                pipestoprocess.Clear();
                if (nodestoadd.Count > 0) pipestoprocess.AddRange(nodestoadd);
            }
            return connectedpipes;
        }
    }
}
