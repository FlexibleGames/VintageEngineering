using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace VintageEngineering.RecipeSystem
{
    public class VERecipeRegistrySystem : ModSystem
    {
        public static bool canRegister = false;

        public List<RecipeMetalPress> MetalPressRecipes = new List<RecipeMetalPress>();        

        public override double ExecuteOrder()
        {
            return 0.6;
        }

        public override void StartPre(ICoreAPI api)
        {
            canRegister = true;
        }

        public override void Start(ICoreAPI api)
        {            
            this.MetalPressRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeMetalPress>>("vemetalpressrecipes").Recipes;
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            ICoreServerAPI sapi = api as ICoreServerAPI;
            if (sapi == null)
            {
                return;
            }                        
        }

        public void RegisterMetalPressRecipe(RecipeMetalPress metalPressRecipe)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register Metal Press recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            metalPressRecipe.RecipeID = MetalPressRecipes.Count + 1;
            this.MetalPressRecipes.Add(metalPressRecipe);
        }

    }
}
