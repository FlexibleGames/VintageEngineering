using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.TemporalStorage
{
    public class TemporalStorageMod : ModSystem
    {
        private ICoreClientAPI _capi;
        private ICoreServerAPI _sapi;

        private IServerNetworkChannel _serverChannel;
        public IClientNetworkChannel  _clientChannel;

        private TemporalStorageData _temporalData;

        public Dictionary<int, HashSet<long>> LidOpenEntityIDByFreqChest = new Dictionary<int, HashSet<long>>();
        public Dictionary<int, HashSet<long>> LidOpenEntityIDByFreqTank = new Dictionary<int, HashSet<long>>();

        public Dictionary<int, InventoryGeneric> ChestStorages => _temporalData._chestStorages;
        public Dictionary<int, InventoryGeneric> TankStorages => _temporalData._tankStorages;
        public Dictionary<int, string> ChestFrequencyUsers => _temporalData._chestFreqUsers;
        public Dictionary<int, string> TankFrequencyUsers => _temporalData._tankFreqUsers;
        public Dictionary<int, List<BlockPos>> ChestPositions => _temporalData._chestPositions;
        public Dictionary<int, List<BlockPos>> TankPositions => _temporalData._tankPositions;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            if (api.Side == EnumAppSide.Server) 
            { 
                _sapi = api as ICoreServerAPI;
                _serverChannel = _sapi.Network.RegisterChannel("vetemporalstorage")
                    .RegisterMessageType(typeof(TemporalStorageData));                    
            }
            else 
            { 
                _capi = api as ICoreClientAPI;
                _clientChannel = _capi.Network.RegisterChannel("vetemporalstorage")
                    .RegisterMessageType(typeof(TemporalStorageData))
                    .SetMessageHandler<TemporalStorageData>(OnStorageDataFromServer);
            }
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            _sapi.Event.GameWorldSave += Event_GameWorldSave;
            _sapi.Event.SaveGameLoaded += Event_SaveGameLoaded;
            _sapi.Event.PlayerNowPlaying += Event_PlayerNowPlaying;
            _sapi.Event.SaveGameCreated += Event_SaveGameCreated;            
        }

        private void Event_SaveGameCreated()
        {
            _temporalData = new TemporalStorageData();
            InitializeData();
        }

        private void InitializeData()
        {
            if (!_temporalData._chestStorages.ContainsKey(0))
            {
                _temporalData._chestStorages.Add(0, new InventoryGeneric(12, "temporalchest-0", _sapi));
                LidOpenEntityIDByFreqChest.Add(0, new HashSet<long>());
            }
            if (!_temporalData._tankStorages.ContainsKey(0))
            {
                _temporalData._tankStorages.Add(0, new InventoryGeneric(1, "temporaltank-0", _sapi, delegate (int id, InventoryGeneric self)
                {
                    return new ItemSlotLiquidOnly(self, 8000);
                }));
                LidOpenEntityIDByFreqTank.Add(0, new HashSet<long>());
            }
            foreach (KeyValuePair<int, InventoryGeneric> pair in _temporalData._chestStorages)
            {
                pair.Value.ResolveBlocksOrItems();
            }
            foreach (KeyValuePair<int, InventoryGeneric> pair in _temporalData._tankStorages)
            {
                pair.Value.ResolveBlocksOrItems();
            }
        }

        public void ChestInventoryOpened(int freq, IPlayer player)
        {
            ChestInventoryOpened(freq, player.Entity.EntityId);
        }
        public void ChestInventoryOpened(int freq, long entityId)
        {
            if (!LidOpenEntityIDByFreqChest.ContainsKey(freq))
            {
                LidOpenEntityIDByFreqChest.Add(freq, new HashSet<long>());
            }
            LidOpenEntityIDByFreqChest[freq].Add(entityId);
        }

        public void ChestInventoryClosed(int freq, IPlayer player)
        {
            ChestInventoryClosed(freq, player.Entity.EntityId);
        }
        public void ChestInventoryClosed(int freq, long entityId)
        {
            LidOpenEntityIDByFreqChest[freq].Remove(entityId);
        }

        public void Event_PlayerNowPlaying(IServerPlayer _splayer)
        {
            _serverChannel.SendPacket<TemporalStorageData>(_temporalData, _splayer);
        }

        public void Event_GameWorldSave()
        {
            byte[] databytes = SerializerUtil.Serialize(_temporalData);
            _sapi.WorldManager.SaveGame.StoreData("temporalstorage", databytes);
            _sapi.Logger.Debug($"VE Temporal Storage: Saved {databytes.Length} bytes to SaveGame");
        }

        public void Event_SaveGameLoaded()
        {
            byte[] databytes = _sapi.WorldManager.SaveGame.GetData("temporalstorage");
            try
            {
                if (databytes != null)
                {
                    _temporalData = SerializerUtil.Deserialize<TemporalStorageData>(databytes);
                    _sapi.Logger.Debug($"VE Temporal Storage: Loaded {databytes.Length} bytes of TemporalStorageData.");
                }
                else
                {
                    _temporalData = new TemporalStorageData();
                }
            }
            catch (Exception e)
            {
                _sapi.Logger.Error($"VE Temporal Storage: Error laoding SaveGame data: {e.ToString()}");
                _temporalData = new TemporalStorageData();
            }
            InitializeData();
        }

        private void OnStorageDataFromServer(TemporalStorageData data)
        {
            _temporalData?.Clean();
            _temporalData = data;
            InitializeData();
        }

        public void RegisterBlocks(ICoreAPI api)
        {

        }
        public void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("VEBETemporalChest", typeof(BlockEntityTemporalContainer));
        }

    }
}
