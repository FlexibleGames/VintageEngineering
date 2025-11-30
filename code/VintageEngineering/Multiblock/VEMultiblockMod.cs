using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.Electrical;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VintageEngineering.Multiblock
{
    public class VEMultiblockMod : ModSystem
    {
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;

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
            RegisterClasses(api);
        }

        private void RegisterClasses(ICoreAPI api)
        {
            // blocks are first
            api.RegisterBlockClass("VEMBDummy", typeof(VEMBDummy));

            // block entities are second
            api.RegisterBlockEntityClass("VEMBEntityDummy", typeof(VEMBEntityDummy));
            api.RegisterBlockEntityClass("MBBEInteractable", typeof(MBBEInteractable));

            // block behaviors are third
            api.RegisterBlockBehaviorClass("VEMultiblockBeh", typeof(VEMultiblockBeh));

            // items are fourth
        }
    }
}
