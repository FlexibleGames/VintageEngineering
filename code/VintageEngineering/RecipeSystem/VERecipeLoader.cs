using System;
using System.Collections.Generic;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

namespace VintageEngineering.RecipeSystem
{    
    public class VERecipeLoader : ModSystem
    {
        ICoreServerAPI sapi;
        private bool classExclusiveRecipes = true;

        public override double ExecuteOrder()
        {
            return 1;
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            sapi = api as ICoreServerAPI;
            if (sapi == null) return;
            classExclusiveRecipes = sapi.World.Config.GetBool("classExclusiveRecipes", true);

            // Now for all the recipe loading...
        }
    }
}
