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
        /// <param name="world"></param>
        /// <param name="net1id"></param>
        /// <param name="net2id"></param>
        public void MergeNetworks(IWorldAccessor world, long net1id, long net2id)
        {

        }
        /// <summary>
        /// Splits a network at the given BlockPos
        /// </summary>
        /// <param name="world"></param>
        /// <param name="pos"></param>
        public void SplitNetwork(IWorldAccessor world, BlockPos pos)
        {
            // check pipe connections for neighboring pipes
        }
    }
}
