using System;
using System.Collections.Generic;
using VintageEngineering.Electrical.Systems;
using VintageEngineering.Electrical.Systems.Catenary;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.Electrical
{
    /// <summary>
    /// Mod to load and manage the Electrical Network Manager
    /// </summary>
    public class ElectricalNetworkMod : ModSystem
    {
        public ICoreAPI api;
        public ICoreServerAPI sapi;
        public ICoreClientAPI capi;       

        /// <summary>
        /// Manager for all Electrical Networks in the current game world.
        /// </summary>
        public ElectricalNetworkManager manager;

        #region ModSystem
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            if (forSide == EnumAppSide.Server)
            {
                return true;
            }
            return false;
        }

        public override void Start(ICoreAPI _api)
        {
            base.Start(api);
            this.api = _api;
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            capi = api as ICoreClientAPI;
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api as ICoreServerAPI;
            manager = new ElectricalNetworkManager(sapi, this);
            manager.InitializeManger();
            api.Event.SaveGameLoaded += this.Event_SaveGameLoaded;
            api.Event.GameWorldSave += this.Event_GameWorldSave;                  
        }

        private void Event_GameWorldSave()
        {
            // This is only run server-side.
            if (manager.networks.Count > 0)
            {
                this.sapi.WorldManager.SaveGame.StoreData("electricalnetworks", manager.NetworkBytes());
                this.sapi.WorldManager.SaveGame.StoreData("electricalnetworknextid", SerializerUtil.Serialize<long>(manager.nextNetworkID));
            }
        }

        private void Event_SaveGameLoaded()
        {
            // This is only run server-side
            byte[] networkbytes = sapi.WorldManager.SaveGame.GetData("electricalnetworks");
            if (networkbytes != null) // null means there are no networks in this world
            {
                byte[] nextidbytes = sapi.WorldManager.SaveGame.GetData("electricalnetworknextid");
                manager.InitializeNetworks(networkbytes, nextidbytes);
            }
        }

        #endregion
    }
}
