using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
            if (_pipeNetworks.Count > 0)
            {
                _sapi.WorldManager.SaveGame.StoreData("pipenetworks", NetworkBytes);
                _sapi.WorldManager.SaveGame.StoreData("pipenetworknextid", SerializerUtil.Serialize(_nextNetworkID));
            }
        }

        private void OnSaveGameLoaded()
        {
            byte[] networkbytes = _sapi.WorldManager.SaveGame.GetData("pipenetworks");
            byte[] nextidbytes = _sapi.WorldManager.SaveGame.GetData("pipenetworknextid");
            if (networkbytes != null) // null means there are no pipe networks in the world yet.
            {
                InitializeNetworkManager(networkbytes, nextidbytes);
            }
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
                return SerializerUtil.Serialize(_pipeNetworks.Values);
            }
            return null;
        }

        public void InitializeNetworkManager(byte[] networks, byte[] nextid)
        {
            _pipeNetworks = SerializerUtil.Deserialize<Dictionary<long, PipeNetwork>>(networks);
            _nextNetworkID = SerializerUtil.Deserialize<long>(nextid);
        }

        public void OnPipeBlockPlaced(IWorldAccessor world, BlockPos pos)
        {
            // check the sides for other pipes and check connection overrides
            // compare all networkID's
            // Merge networks if needed create network if needed, set pos networkid
        }

        public void OnPipeBlockBroken(IWorldAccessor world, BlockPos pos)
        {
            // check connection sides for pipe connections
            // if endpoint, remove node; otherwise split network
        }

        /// <summary>
        /// Called when the player force-removes a pipe-pipe connection
        /// potentially splitting the network.
        /// </summary>
        /// <param name="world">World Accessor</param>
        /// <param name="pos">Position of the override.</param>
        public void OnPipeConnectionOverride(IWorldAccessor world, BlockPos pos)
        {

        }
        /// <summary>
        /// Merge Pipe Network net2 into net1.
        /// </summary>
        /// <param name="world"></param>
        /// <param name="net1"></param>
        /// <param name="net2"></param>
        public void MergeNetworks(IWorldAccessor world, PipeNetwork net1, PipeNetwork net2)
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
