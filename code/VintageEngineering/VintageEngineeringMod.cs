﻿using System;
using Vintagestory.API.Common;
using Vintagestory.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using VintageEngineering.Electrical;
using VintageEngineering.Transport;
using VintageEngineering.Transport.Pipes;
using HarmonyLib;
using System.Collections.Generic;
using Vintagestory.GameContent;
using System.IO;
using VintageEngineering.blockentity;
using VintageEngineering.Blocks;
using VintageEngineering.blockBhv;

[assembly: ModInfo("VintageEngineering",
                    Authors = new string[] { "Flexible Games", "bluelightning32" },
                    Description = "Late game tech, automation, power, and mining.",
                    Version = "1.0.0")]

namespace VintageEngineering
{
    public class VintageEngineeringMod : ModSystem
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;
        private Harmony harmony;

        public IClientNetworkChannel capi_vechannel;
        public IServerNetworkChannel sapi_vechannel;

        public List<PipeFilterGuiElement> _pipeFilterList = new List<PipeFilterGuiElement>();        

        public bool _filterListLoaded = false;

        #region Config Related
        private VintEngCommonConfig _commonConfig;
        private static string _commonConfigFilename = "vinteng_common.json";
        public VintEngCommonConfig CommonConfig 
        {
            get
            {
                return _commonConfig;
            }
        }
        public static VintEngCommonConfig ReadConfig(ICoreAPI api)
        {
            VintEngCommonConfig tmpconfig;
            try
            {
                tmpconfig = api.LoadModConfig<VintEngCommonConfig>(_commonConfigFilename);
                if (tmpconfig == null)
                {
                    tmpconfig = new VintEngCommonConfig();
                    api.StoreModConfig<VintEngCommonConfig>(tmpconfig, _commonConfigFilename);                    
                }
                else
                {
                    api.StoreModConfig<VintEngCommonConfig>(new VintEngCommonConfig(tmpconfig), _commonConfigFilename);
                    tmpconfig = api.LoadModConfig<VintEngCommonConfig>(_commonConfigFilename);
                }
            }
            catch (Exception e)
            {
                api.Logger.Error("VintEng: Config file exception; Typo or invalid value. Rebuilding Config. Exception: " + e);
                tmpconfig = new VintEngCommonConfig();
                api.StoreModConfig<VintEngCommonConfig>(tmpconfig, _commonConfigFilename);
            }
            return tmpconfig;
        }
        #endregion

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);
            if (api is ICoreServerAPI)
            {
                _commonConfig = ReadConfig(api);
                api.World.Config.SetBool("VintEng_GenOilDeposit", _commonConfig.OilGyser_GenOilDeposit);
            }
        }

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            string patchId = Mod.Info.ModID;
            if (!Harmony.HasAnyPatches(patchId))
            {
                harmony = new Harmony(patchId);
                harmony.PatchAll();
            }

            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
                capi.Event.LevelFinalize += OnLevelFinalize;
                capi_vechannel = capi.Network.RegisterChannel("vepipefiltersync")
                    .RegisterMessageType(typeof(PipeFilterPacket));
            }
            else
            {
                sapi = api as ICoreServerAPI;
                sapi_vechannel = sapi.Network.RegisterChannel("vepipefiltersync")
                    .RegisterMessageType(typeof(PipeFilterPacket))
                    .SetMessageHandler<PipeFilterPacket>(OnFilterDataSyncFromClient);
            }
            RegisterItems(api);
            RegisterBlocks(api);
            RegisterBlockEntities(api);
            RegisterBlockEntityBehaviors(api);
        }

        private void OnFilterDataSyncFromClient(IServerPlayer fromPlayer, PipeFilterPacket packet)
        {
            if (packet != null)
            {
                if (fromPlayer != null && fromPlayer.InventoryManager != null && fromPlayer.InventoryManager.ActiveHotbarSlot != null)
                {
                    if (fromPlayer.InventoryManager.ActiveHotbarSlot.Empty) return;

                    fromPlayer.InventoryManager.ActiveHotbarSlot.Itemstack.FromBytes(new BinaryReader(new MemoryStream(packet.SyncedStack)));
                    fromPlayer.InventoryManager.ActiveHotbarSlot.MarkDirty();
                }
            }
        }

        /// <summary>
        /// Called on the CLIENT on having received the level finalized packet.
        /// </summary>
        private void OnLevelFinalize()
        {
            TyronThreadPool.QueueTask(new Action(LoadFilterEntries), "Vintage Engineering Pipe Filter List");
            capi.Settings.AddWatcher<float>("guiScale", OnScaleChanged);
        }

        /// <summary>
        /// Called on a seperate thread to build the master list of Block and Item entries for Pipe Filter search feature.
        /// </summary>
        private void LoadFilterEntries()
        {         
            foreach (CollectibleObject obj in capi.World.Collectibles)
            {
                List<ItemStack> stacks = obj.GetHandBookStacks(capi);
                if (stacks != null)
                {
                    foreach (ItemStack stack in stacks)
                    {
                        _pipeFilterList.Add(new PipeFilterGuiElement(capi, stack));
                    }
                }
            }
            _filterListLoaded = true;
        }
        // Called if the GuiScale setting is changed on the client.
        private void OnScaleChanged(float scale)
        {
            if (_filterListLoaded && _pipeFilterList.Count > 0)
            {
                foreach (PipeFilterGuiElement entry in _pipeFilterList)
                {
                    entry.Dispose();
                }
            }
        }

        public void RegisterItems(ICoreAPI api)
        {
            api.RegisterItemClass("VEPipeUpgrade", typeof(ItemPipeUpgrade));
            api.RegisterItemClass("VEPipeFilter", typeof(ItemPipeFilter));
        }

        public void RegisterBlocks(ICoreAPI api)
        {
            // generic electric block, can be used for most machines
            api.RegisterBlockClass("VEElectricBlock", typeof(ElectricBlock));

            // LVGenerator has neighbor side power distribution
            api.RegisterBlockClass("VELVGenerator", typeof(BlockLVGenerator));

            // Needed for neighbor block change event
            api.RegisterBlockClass("VEForge", typeof(BlockVEForge));

            api.RegisterBlockClass("VEElectricKinetic",typeof(BlockElectricKinetic));

            // mixer has fluids
            api.RegisterBlockClass("VEBlockFluidIO", typeof(BlockFluidIO));

            api.RegisterBlockClass("VELVBlower", typeof(BlockLVBlower));

            api.RegisterBlockClass("VEPipeBlock", typeof(BlockPipeBase));

            api.RegisterBlockClass("VEBlockFluidTank", typeof(BlockFluidTank));

            api.RegisterBlockClass("VEBlockCrudeOil", typeof(BlockCrudeOil));
            api.RegisterBlockClass("VEBlockCrudeOilWell", typeof(BlockCrudeOilWell));
        }
        public void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("VEBERelay", typeof(ElectricBERelay));

            api.RegisterBlockEntityClass("VEBELVGenerator", typeof(BELVGenerator));
            api.RegisterBlockEntityClass("VEBEMetalPress", typeof(BEMetalPress));
            api.RegisterBlockEntityClass("VEBELogSplitter", typeof(BELogSplitter));
            api.RegisterBlockEntityClass("VEBEExtruder", typeof(BEExtruder));
            api.RegisterBlockEntityClass("VEBESawmill", typeof(BESawmill));
            api.RegisterBlockEntityClass("VEBECrusher", typeof(BECrusher));
            api.RegisterBlockEntityClass("VEBEKiln", typeof(BEKiln));
            api.RegisterBlockEntityClass("VEBEForge", typeof(BEForge));
            api.RegisterBlockEntityClass("VEBEElectricKinetic",typeof(BEElectricKinetic));
            api.RegisterBlockEntityClass("VEBECNC", typeof(BECNC));
            api.RegisterBlockEntityClass("VEBEMixer", typeof(BEMixer));
            api.RegisterBlockEntityClass("VEBELVBattery", typeof(BELVBattery));
            api.RegisterBlockEntityClass("VEBELVCharger", typeof(BELVCharger));
            api.RegisterBlockEntityClass("VEBECreosoteOven", typeof(BECreosoteOven));
            api.RegisterBlockEntityClass("VEBEBlastFurnace", typeof(BEBlastFurnace));
            api.RegisterBlockEntityClass("VEBEBlower", typeof(BEBlower));

            api.RegisterBlockEntityClass("VEBEItemPipe", typeof(BEPipeItem));
            api.RegisterBlockEntityClass("VEBEFluidPipe", typeof(BEPipeFluid));
            api.RegisterBlockEntityClass("VEBEFluidTank", typeof(BEFluidTank));
            api.RegisterBlockEntityClass("VEBELVPump", typeof(BELVPump));

            api.RegisterBlockEntityClass("VEBECrudeOilWell", typeof(BECrudeOilWell));
        }

        public void RegisterBlockEntityBehaviors(ICoreAPI api)
        {
            api.RegisterBlockEntityBehaviorClass("VEElectricMotorBhv", typeof(ElectricKineticMotorBhv));
            api.RegisterBlockEntityBehaviorClass("VEElectricKineticGenBhv", typeof(ElectricKineticAlternatorBhv));
        }

        public override void Dispose()
        {
            base.Dispose();
            harmony?.UnpatchAll(harmony.Id);
        }
    }

}
