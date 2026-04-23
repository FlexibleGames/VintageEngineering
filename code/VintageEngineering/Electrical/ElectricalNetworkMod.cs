using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        //public ICoreClientAPI _capi;       

        /// <summary>
        /// Manager for all Electrical Networks in the current game world.
        /// </summary>
        public ElectricalNetworkManager manager;

        #region Config Related
        private ElectricalNetworkConfig _electricConfig;
        private static string _electricConfigFilename = "vinteng_electric.json";
        public ElectricalNetworkConfig ElectricConfig
        {
            get
            {
                return _electricConfig;
            }
        }
        public static ElectricalNetworkConfig ReadConfig(ICoreAPI api)
        {
            ElectricalNetworkConfig tmpconfig;
            try
            {
                tmpconfig = api.LoadModConfig<ElectricalNetworkConfig>(_electricConfigFilename);
                if (tmpconfig == null)
                {
                    tmpconfig = new ElectricalNetworkConfig();
                    api.StoreModConfig<ElectricalNetworkConfig>(tmpconfig, _electricConfigFilename);
                }
                else
                {
                    api.StoreModConfig<ElectricalNetworkConfig>(new ElectricalNetworkConfig(tmpconfig), _electricConfigFilename);
                    tmpconfig = api.LoadModConfig<ElectricalNetworkConfig>(_electricConfigFilename);
                }
            }
            catch (Exception e)
            {
                api.Logger.Error("VintEng: Electric Config file exception; Typo or invalid value. Rebuilding Config. Exception: " + e);
                tmpconfig = new ElectricalNetworkConfig();
                api.StoreModConfig<ElectricalNetworkConfig>(tmpconfig, _electricConfigFilename);
            }
            return tmpconfig;
        }
        #endregion

        #region ModSystem
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            // All of the network simulation runs on the server side. The mod also needs to start on the client side
            // so that the block entity behaviors are registered. The GUIs on the client side access the block entity
            // behaviors.
            return true;
        }

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            if (api is ICoreServerAPI)
            { 
                _electricConfig = ReadConfig(api);
            }
        }

        public override void Start(ICoreAPI _api)
        {
            base.Start(api);
            this.api = _api;
            RegisterBlockEntityBehaviors(api);
            RegisterBlockEntities(api);
        }

        private void RegisterBlockEntityBehaviors(ICoreAPI api)
        {
            api.RegisterBlockEntityBehaviorClass("Electric", typeof(ElectricBEBehavior));
        }

        private void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("VEBERelay", typeof(ElectricBERelay));
            api.RegisterBlockEntityClass("VEBEMBPowerConnector", typeof(BEMBPowerConnector));
        }

        //public override void StartClientSide(ICoreClientAPI api)
        //{
        //    base.StartClientSide(api);
        //    _capi = api as ICoreClientAPI;
        //}

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            sapi = api as ICoreServerAPI;
            manager = new ElectricalNetworkManager(sapi, this);
            manager.InitializeManger();
            api.Event.SaveGameLoaded += this.Event_SaveGameLoaded;
            api.Event.GameWorldSave += this.Event_GameWorldSave;
            SetupDebugCommands();
        }

        private void Event_GameWorldSave()
        {
            // This is only run server-side.
//            if (manager.networks.Count > 0)
//            {
            this.sapi.WorldManager.SaveGame.StoreData("electricalnetworks", manager.NetworkBytes());
            this.sapi.WorldManager.SaveGame.StoreData("electricalnetworknextid", SerializerUtil.Serialize<long>(manager.nextNetworkID));
//            }
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

        #region DebugCommands
        private void SetupDebugCommands()
        {
            IChatCommandApi chatCommands = sapi.ChatCommands;
            CommandArgumentParsers parsers = sapi.ChatCommands.Parsers;

            chatCommands.GetOrCreate("vedebug")
                .WithDescription("Vintage Engineering Debug Commands")
                .RequiresPrivilege(Privilege.controlserver)
                .BeginSubCommand("validatepowernetworks")
                .WithAlias(["vpn"])
                .WithDescription("Validate all nodes of all electric networks. Optionally DELETES invalid networks.")
                .WithArgs([parsers.OptionalBool("dodelete", "delete")])
                .HandleWith(new OnCommandDelegate(ValidateAllNetworks)).EndSubCommand()
                .BeginSubCommand("validate")
                .WithDescription("Validate a single given electric network ID. With optional delete.")
                .WithArgs([parsers.Long("networkID"), parsers.OptionalBool("dodelete", "delete")])
                .HandleWith(new OnCommandDelegate(ValidateNetwork)).EndSubCommand();
        }

        private TextCommandResult ValidateAllNetworks(TextCommandCallingArgs args)
        {
            // Needs to validate all networks

            if (manager.networks.Count == 0)
            {
                return TextCommandResult.Success("No Electric Networks exist to validate.");
            }
            Stopwatch timer = Stopwatch.StartNew();
            int numInvalid = manager.ValidateNetworks((bool)args[0]);
            timer.Stop();
            string dodelete = "";
            if ((bool)args[0])
            {
                dodelete = " which were deleted.";
            }
            else
            {
                dodelete = ".";
            }
            return TextCommandResult.Success($"Electric Network validations took {timer.ElapsedMilliseconds}ms and found {numInvalid} invalid networks{dodelete}");
        }

        /// <summary>
        /// Triggers a Electric Network validation on a given NetworkID in args.<br/>
        /// Called via a debug chat command.
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        private TextCommandResult ValidateNetwork(TextCommandCallingArgs args)
        {
            // Needs to validate given network in args
            if (args.Parsers[0].IsMissing)
            {
                return TextCommandResult.Success("Missing NetworkID to validate.");
            }
            long netID = (long)args[0];
            if (!manager.networks.ContainsKey(netID))
            {
                return TextCommandResult.Success($"Electric NetworkID {netID} does not exist.");
            }
            bool isValid = manager.ValidateNetwork(netID);
            if (isValid)
            {
                return TextCommandResult.Success($"Network ID {netID} has valid nodes.");
            }
            else
            {
                //manager.DeleteNetwork(netID);
                string dodelete = "";
                if ((bool)args[1])
                {
                    dodelete = " Deleting...";
                    manager.DeleteNetwork(netID);
                }
                return TextCommandResult.Success($"Network ID {netID} Failed Validation. All nodes are invalid. {dodelete}");
            }
        }
        #endregion
    }
}
