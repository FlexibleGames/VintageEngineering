using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using VintageEngineering.API;
using VintageEngineering.blockentity;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.TemporalStorage
{
    public class TemporalStorageMod : ModSystem
    {
        private ICoreAPI _api;
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
            _api = api;
            if (api.Side == EnumAppSide.Server) 
            { 
                _sapi = api as ICoreServerAPI;
                _serverChannel = _sapi.Network.RegisterChannel("vetemporalstorage")
                    .RegisterMessageType(typeof(TemporalStorageData))
                    .RegisterMessageType(typeof(TemporalInventoryUpdate));
            }
            else 
            { 
                _capi = api as ICoreClientAPI;
                _clientChannel = _capi.Network.RegisterChannel("vetemporalstorage")
                    .RegisterMessageType(typeof(TemporalStorageData))
                    .SetMessageHandler<TemporalStorageData>(OnStorageDataFromServer)
                    .RegisterMessageType(typeof(TemporalInventoryUpdate))
                    .SetMessageHandler<TemporalInventoryUpdate>(OnTemporalInventoryUpdate);
            }
            RegisterBlockBehaviors(api);
            RegisterBlockEntities(api);
        }

        private void OnTemporalInventoryUpdate(TemporalInventoryUpdate packet)
        {
            TreeAttribute inv = TreeAttribute.CreateFromBytes(packet._inventoryData);
            if (_temporalData == null) return;
            if (packet._isChest)
            {

            }
            else
            {
                if (_temporalData._tankStorages.ContainsKey(packet._frequency))
                {
                    _temporalData._tankStorages[packet._frequency].FromTreeAttributes(inv);
                }
                else
                {
                    _temporalData._tankStorages.Add(packet._frequency, new InventoryGeneric(_capi));
                    _temporalData._tankStorages[packet._frequency].FromTreeAttributes(inv);
                }
                TankModified(packet._frequency, null);
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
            InitializeData(_sapi);
        }

        private void InitializeData(ICoreAPI api)
        {
            if (!_temporalData._chestStorages.ContainsKey(0))
            {
                _temporalData._chestStorages.Add(0, new InventoryGeneric(12, "temporalchest-0", _sapi));
                if (!LidOpenEntityIDByFreqChest.ContainsKey(0)) LidOpenEntityIDByFreqChest.Add(0, new HashSet<long>());
            }
            if (!_temporalData._tankStorages.ContainsKey(0))
            {
                _temporalData._tankStorages.Add(0, new InventoryGeneric(1, "temporaltank-0", _sapi, delegate (int id, InventoryGeneric self)
                {
                    return new ItemSlotLargeLiquid(self, 10000);
                }));
                if (!LidOpenEntityIDByFreqTank.ContainsKey(0)) LidOpenEntityIDByFreqTank.Add(0, new HashSet<long>());
            }
            if (!_temporalData._chestPositions.ContainsKey(0))
            {
                _temporalData._chestPositions.Add(0, new List<BlockPos>());
            }
            if (!_temporalData._tankPositions.ContainsKey(0))
            {
                _temporalData._tankPositions.Add(0, new List<BlockPos>());
            }
            if (_temporalData._chestPositions[0] == null) _temporalData._chestPositions[0] = new List<BlockPos>();
            if (_temporalData._tankPositions[0] == null) _temporalData._tankPositions[0] = new List<BlockPos>();

            foreach (KeyValuePair<int, byte[]> pair in _temporalData._chestStorageData)
            {
                InventoryGeneric inv;
                if (!_temporalData._chestStorages.TryGetValue(pair.Key, out inv))
                {
                    inv = new InventoryGeneric(12, "temporalchest-" + pair.Key, api);
                    _temporalData._chestStorages[pair.Key] = inv;
                }

                TreeAttribute tree = new TreeAttribute();
                tree.FromBytes(pair.Value);
                inv.FromTreeAttributes(tree);
                inv.LateInitialize("temporalchest-" + pair.Key, api);
                //inv.ResolveBlocksOrItems();

                if (!LidOpenEntityIDByFreqChest.ContainsKey(pair.Key))
                {
                    LidOpenEntityIDByFreqChest[pair.Key] = new HashSet<long>();
                }
            }
            foreach (KeyValuePair<int, byte[]> pair in _temporalData._tankStorageData)
            {
                InventoryGeneric inv;
                if (!_temporalData._tankStorages.TryGetValue(pair.Key, out inv))
                {
                    inv = new InventoryGeneric(12, "temporaltank-" + pair.Key, api, delegate (int id, InventoryGeneric self)
                    {
                        return new ItemSlotLargeLiquid(self, 10000);
                    });
                    _temporalData._tankStorages[pair.Key] = inv;
                }

                TreeAttribute tree = new TreeAttribute();
                tree.FromBytes(pair.Value);
                inv.FromTreeAttributes(tree);
                inv.LateInitialize("temporaltank-" + pair.Key, api);
                //inv.ResolveBlocksOrItems();

                if (!LidOpenEntityIDByFreqTank.ContainsKey(pair.Key))
                {
                    LidOpenEntityIDByFreqTank[pair.Key] = new HashSet<long>();
                }
            }
        }

        public void NewTemporalInventory(int freq, ICoreAPI api, bool isChest = true)
        {
            if (isChest)
            {
                _temporalData._chestStorages.Add(freq, new InventoryGeneric(12, "temporalchest-" + freq, api));
                _temporalData._chestStorages[freq].LateInitialize("temporalchest-" + freq, api);
            }
            else
            {
                _temporalData._tankStorages.Add(freq, new InventoryGeneric(1, "temporaltank-" + freq, api, delegate(int id, InventoryGeneric self)
                {
                    return new ItemSlotLargeLiquid(self, 10000);
                }));
                _temporalData._tankStorages[freq].LateInitialize("temporaltank-" + freq, api);                
            }
            if (api is ICoreServerAPI sapi)
            {
                _serverChannel.SendPacket<TemporalStorageData>(_temporalData, (IServerPlayer[])_sapi.World.AllOnlinePlayers);
            }
        }

        public void TankModified(int freq, BlockPos skipPos)
        {
            if (_sapi != null)
            {
                TemporalInventoryUpdate tankupdate = new TemporalInventoryUpdate(_temporalData._tankStorages[freq], freq, false);
                _serverChannel.SendPacket<TemporalInventoryUpdate>(tankupdate, _sapi.World.AllOnlinePlayers as IServerPlayer[]);
            }
            if (_capi == null) return;

            if (_temporalData._tankStorages.ContainsKey(freq))
            {
                if (_temporalData._tankPositions.ContainsKey(freq))
                {
                    List<BlockPos> tanks = _temporalData._tankPositions[freq];
                    foreach (BlockPos tank in tanks)
                    {
                        if (skipPos != null && tank == skipPos) continue;
                        BEFluidTank betank = _capi.World.BlockAccessor.GetBlockEntity<BEFluidTank>(tank);
                        if (betank != null)
                        {
                            betank.RegenFluidMesh();
                        }
                    }
                }                                
            }
        }

        /// <summary>
        /// Helps track LidOpenEntityID per freqency inventories
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="player"></param>
        public void ChestInventoryOpened(int freq, IPlayer player)
        {
            ChestInventoryOpened(freq, player.Entity.EntityId);
        }
        /// <summary>
        /// Helps track LidOpenEntityID per freqency inventories
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="entityId"></param>
        public void ChestInventoryOpened(int freq, long entityId)
        {
            if (!LidOpenEntityIDByFreqChest.ContainsKey(freq))
            {
                LidOpenEntityIDByFreqChest.Add(freq, new HashSet<long>());
            }
            LidOpenEntityIDByFreqChest[freq].Add(entityId);
        }
        /// <summary>
        /// Helps track LidOpenEntityID per freqency inventories
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="player"></param>
        public void ChestInventoryClosed(int freq, IPlayer player)
        {
            ChestInventoryClosed(freq, player.Entity.EntityId);
        }
        /// <summary>
        /// Helps track LidOpenEntityID per freqency inventories
        /// </summary>
        /// <param name="freq"></param>
        /// <param name="entityId"></param>
        public void ChestInventoryClosed(int freq, long entityId)
        {
            if (LidOpenEntityIDByFreqChest.ContainsKey(freq))
            { 
                LidOpenEntityIDByFreqChest[freq].Remove(entityId); 
            }
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
            InitializeData(_sapi);
        }

        private void OnStorageDataFromServer(TemporalStorageData data)
        {
            _temporalData?.Clean();
            _temporalData = data;
            InitializeData(_capi);
        }

        public void RegisterBlocks(ICoreAPI api)
        {

        }

        public void RegisterBlockBehaviors(ICoreAPI api)
        {
            api.RegisterBlockBehaviorClass("VETemporalContainerBeh", typeof(BlockBehTemporalContainer));
        }

        public void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("VEBETemporalChest", typeof(BlockEntityTemporalContainer));
            api.RegisterBlockEntityClass("VEBETemporalTank", typeof(BETemporalTank));
        }

    }
}
