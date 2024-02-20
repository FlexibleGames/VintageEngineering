using System;
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
        ICoreAPI api;
        ICoreServerAPI sapi;
        ICoreClientAPI capi;
        CatenaryMod cm;

        #region ModSystem
        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return true;
        }
        public override void Start(ICoreAPI _api)
        {
            base.Start(api);
            this.api = _api;
            cm = api.ModLoader.GetModSystem<CatenaryMod>();
            if (_api.Side == EnumAppSide.Client) capi = api as ICoreClientAPI;
            else sapi = api as ICoreServerAPI;
        }


        #endregion
    }
}
