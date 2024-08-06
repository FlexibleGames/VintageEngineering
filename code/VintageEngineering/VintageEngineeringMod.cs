using System;
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

        public List<PipeFilterGuiElement> _pipeFilterList = new List<PipeFilterGuiElement>();
        //public List<PipeFilterGuiElement> _shownFilterElements = new List<PipeFilterGuiElement>();

        public bool _filterListLoaded = false;

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
            }
            else
            {
                sapi = api as ICoreServerAPI;
            }
            RegisterItems(api);
            RegisterBlocks(api);
            RegisterBlockEntities(api);
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

            // mixer has fluids
            api.RegisterBlockClass("VEMixer", typeof(BlockMixer));

            api.RegisterBlockClass("VEPipeBlock", typeof(BlockPipeBase));
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
            api.RegisterBlockEntityClass("VEBECNC", typeof(BECNC));
            api.RegisterBlockEntityClass("VEBEMixer", typeof(BEMixer));

            api.RegisterBlockEntityClass("VEBEItemPipe", typeof(BEPipeItem));
            //api.RegisterBlockEntityClass("VEBEFluidPipe", typeof(BEPipeFluid));
        }

        public override void Dispose()
        {
            base.Dispose();
            harmony?.UnpatchAll(harmony.Id);
        }
    }

}
