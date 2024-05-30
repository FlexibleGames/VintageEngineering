using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VintageEngineering.Transport
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
        {  get { return _nextNetworkID; } }

        public byte[] NetworkBytes()
        {
            if (_pipeNetworks.Count > 0)
            {
                foreach (KeyValuePair<long, PipeNetwork> net in _pipeNetworks)
                {
                    net.Value.NetworkID = net.Key;
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

        }

        public void OnPipeBlockBroken(IWorldAccessor world, BlockPos pos)
        {

        }
    }
}
