using System;
using Vintagestory.API.Common;
using Vintagestory.Common;
using Vintagestory.API.Client;
using Vintagestory.API.Server;

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
            api.RegisterBlockClass("VETestGen", typeof(BlockTestGen));
            api.RegisterBlockClass("VETestMachine", typeof(BlockTestMachine));
        }
        public void RegisterBlockEntities(ICoreAPI api)
        {
            api.RegisterBlockEntityClass("VEBETestGen", typeof(BETestGen));
            api.RegisterBlockEntityClass("VEBETestMachine", typeof(BETestMachine));
        }
    }

}
