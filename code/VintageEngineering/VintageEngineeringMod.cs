using System;
using Vintagestory.API.Common;
using Vintagestory.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;
using VintageEngineering.Electrical;

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
            api.RegisterBlockClass("VEElectricBlock", typeof(ElectricBlock)); // generic electric block

            api.RegisterBlockClass("VETestGen", typeof(BlockTestGen));
            api.RegisterBlockClass("VETestMachine", typeof(BlockMetalPress));
        }
        public void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("VEBERelay", typeof(ElectricBERelay));

            api.RegisterBlockEntityClass("VEBETestGen", typeof(BETestGen));
            api.RegisterBlockEntityClass("VEBETestMachine", typeof(BEMetalPress));
        }
    }

}
