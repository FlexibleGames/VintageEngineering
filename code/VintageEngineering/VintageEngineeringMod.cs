using System;
using Vintagestory.API.Common;
using Vintagestory.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using VintageEngineering.Electrical;
using Vintagestory.GameContent;

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
            RegisterBlocks(api);
            RegisterBlockEntities(api);
            
        }

        public void RegisterBlocks(ICoreAPI api)
        {
            // generic electric block, can be used for most machines
            api.RegisterBlockClass("VEElectricBlock", typeof(ElectricBlock)); 

            api.RegisterBlockClass("VELVGenerator", typeof(BlockLVGenerator));
            api.RegisterBlockClass("VEMetalPress", typeof(BlockMetalPress));
            api.RegisterBlockClass("VELogSplitter", typeof(BlockLogSplitter));
            api.RegisterBlockClass("VEExtruder", typeof(BlockExtruder));
            api.RegisterBlockClass("VESawmill", typeof(BlockSawmill));
            api.RegisterBlockClass("VECrusher", typeof(BlockCrusher));
            api.RegisterBlockClass("VEKiln", typeof(BlockKiln));
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
        }
    }

}
