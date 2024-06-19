using System;
using Vintagestory.API.Common;
using Vintagestory.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using VintageEngineering.Electrical;
using VintageEngineering.Transport;
using VintageEngineering.Transport.Pipes;

[assembly: ModInfo("VintageEngineering",
                    Authors = new string[] { "Flexible Games" },
                    Description = "Late game tech, automation, power, and mining.",
                    Version = "1.0.0")]

namespace VintageEngineering
{
    public class VintageEngineeringMod : ModSystem
    {
        ICoreClientAPI capi;
        ICoreServerAPI sapi;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
            }
            else
            {
                sapi = api as ICoreServerAPI;                
            }
            RegisterItems(api);
            RegisterBlocks(api);
            RegisterBlockEntities(api);            
        }

        public void RegisterItems(ICoreAPI api)
        {
            api.RegisterItemClass("VEPipeUpgrade", typeof(ItemPipeUpgrade));
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
    }

}
