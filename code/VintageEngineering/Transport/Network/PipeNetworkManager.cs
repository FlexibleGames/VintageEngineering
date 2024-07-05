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

        public void OnPipeBlockPlaced(IWorldAccessor world, BlockPos pos)
        {
            // check the sides for other pipes and check connection overrides
            // compare all networkID's
            // Merge networks if needed create network if needed, set pos networkid
            BEPipeBase us = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
            if (us == null) return;
            BlockPipeBase usb = world.BlockAccessor.GetBlock(pos) as BlockPipeBase;
            if (usb == null) return;
            int pipecons = 0;
            bool hasinserts = false;
            if (us != null)
            {
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
                    if (_pipeNetworks == null) _pipeNetworks = new Dictionary<long, PipeNetwork>();
                    _pipeNetworks.Add(_nextNetworkID, new PipeNetwork(_nextNetworkID, usb.PipeUse));
                    _pipeNetworks[_nextNetworkID].AddPipe(pos.Copy(), world);
                    _nextNetworkID++;
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
                SplitNetwork(world, pos);
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
                _pipeNetworks[net1id].AddPipe(pos, world);
            }
            _pipeNetworks[net2id].Clear();
            _pipeNetworks.Remove(net2id);
        }
        /// <summary>
        /// Splits a network at the given BlockPos
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pos">Pipe Block being removed.</param>
        public void SplitNetwork(IWorldAccessor world, BlockPos pos)
        {
            // check pipe connections for neighboring pipes
            List<BlockPos> connectedpipes = new List<BlockPos>();

            BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;
            BlockPipeBase pipeblock = world.BlockAccessor.GetBlock(pos) as BlockPipeBase;
            if (bep == null || pipeblock == null) return; // something is wrong

            long splitid = bep.NetworkID;

            for (int f = 0; f < 6; f++)
            {
                if (bep.ConnectionSides[f])
                {
                    connectedpipes.Add(pos.AddCopy(BEPipeBase.ConvertIndexToFace(f)));
                    bep.OverridePipeConnectionFace(f, true); // disconnect the face to avoid false connections.
                }
            }
            // connectedpipes now has a list of all the pipe block positions of new (potential) networks.
            // the connections of the position passed in have also been disabled to prevent false connections.
            if (connectedpipes.Count > 0)
            {
                foreach (BlockPos newnet in connectedpipes)
                {
                    // TODO all the stuff in here...
                }
            }
        }

        /// <summary>
        /// Returns List of BlockPos of all the pipes connected to the given pos.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pos">Start Position to check from.</param>
        /// <returns>List of BlockPos of all connected pipes.</returns>
        public List<BlockPos> GetConnectedPipes(IWorldAccessor world, BlockPos pos)
        {
            List<BlockPos> connectedpipes = new List<BlockPos>();
            List<BlockPos> pipestoprocess = new List<BlockPos>();

            BEPipeBase bep = world.BlockAccessor.GetBlockEntity(pos) as BEPipeBase;

            connectedpipes.Add(pos);
            pipestoprocess.AddRange(bep.GetPipeConnections());

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
                    nodestoadd.AddRange(pipe.GetPipeConnections());
                }
                pipestoprocess.Clear();
                if (nodestoadd.Count > 0) pipestoprocess.AddRange(nodestoadd);
            }
            return connectedpipes;
        }
    }
}
