using System;
using System.Collections.Generic;
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

        /// <summary>
        /// Turn metal things into other metal things
        /// </summary>
        public List<RecipeMetalPress>   MetalPressRecipes = new List<RecipeMetalPress>(); 
        /// <summary>
        /// Turn logs into things
        /// </summary>
        public List<RecipeLogSplitter>  LogSplitterRecipes = new List<RecipeLogSplitter>();
        /// <summary>
        /// Turn wood into other wood things
        /// </summary>
        public List<RecipeSawMill>      SawMillRecipes = new List<RecipeSawMill>();
        /// <summary>
        /// Crush things into smaller things
        /// </summary>
        public List<RecipeCrusher>      CrusherRecipes = new List<RecipeCrusher>();
        /// <summary>
        /// Grind small things into even smaller things.
        /// </summary>
        public List<RecipeGrinder>      GrinderRecipes = new List<RecipeGrinder>();
        /// <summary>
        /// Mix things together to make other things.
        /// </summary>
        public List<RecipeMixer>        MixerRecipes = new List<RecipeMixer>();
        /// <summary>
        /// Make Wire and things.
        /// </summary>
        public List<RecipeExtruder>     ExtruderRecipes = new List<RecipeExtruder>();        
        /// <summary>
        /// Automate complex things en masse.
        /// </summary>
        public List<RecipeCNC>          CNCRecipes = new List<RecipeCNC>();
        /// <summary>
        /// Bake small things into other things.
        /// </summary>
        public List<RecipeKiln>         KilnRecipes = new List<RecipeKiln>();
        /// <summary>
        /// Turn Combustable things into other things
        /// </summary>
        public List<RecipeCokeOven>     CokeOvenRecipes = new List<RecipeCokeOven>();
        /// <summary>
        /// Smelt things into other things
        /// </summary>
        public List<RecipeAlloyOven>    AlloyOvenRecipes = new List<RecipeAlloyOven>();

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
            this.LogSplitterRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeLogSplitter>>("velogsplitterrecipes").Recipes;
            this.SawMillRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeSawMill>>("vesawmillrecipes").Recipes;

            this.ExtruderRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeExtruder>>("veextruderrecipes").Recipes;
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

        public void RegisterLogSplitterRecipe(RecipeLogSplitter recipeLogSplitter)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register Log Splitter recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeLogSplitter.RecipeID = LogSplitterRecipes.Count + 1;
            this.LogSplitterRecipes.Add(recipeLogSplitter);
        }

        public void RegisterSawMillRecipe(RecipeSawMill recipeSawMillRecipe)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register Saw Mill recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeSawMillRecipe.RecipeID = SawMillRecipes.Count + 1;
            this.SawMillRecipes.Add(recipeSawMillRecipe);
        }

        public void RegisterCrusherRecipe(RecipeCrusher recipeCrusherRecipe)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register Crusher recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeCrusherRecipe.RecipeID = CrusherRecipes.Count + 1;
            this.CrusherRecipes.Add(recipeCrusherRecipe);
        }

        public void RegisterGrinderRecipe(RecipeGrinder recipeGrinderRecipe)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register Grinder recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeGrinderRecipe.RecipeID = GrinderRecipes.Count + 1;
            this.GrinderRecipes.Add(recipeGrinderRecipe);
        }

        public void RegisterMixerRecipe(RecipeMixer recipeMixerRecipe)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register Mixer recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeMixerRecipe.RecipeID = MixerRecipes.Count + 1;
            this.MixerRecipes.Add(recipeMixerRecipe);
        }

        public void RegisterExtruderRecipe(RecipeExtruder recipeExtruderRecipe)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register Extruder recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeExtruderRecipe.RecipeID = ExtruderRecipes.Count + 1;
            this.ExtruderRecipes.Add(recipeExtruderRecipe);
        }

        public void RegisterCNCRecipe(RecipeCNC recipeCNC)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register CNC recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeCNC.RecipeID = CNCRecipes.Count + 1;
            this.CNCRecipes.Add(recipeCNC);
        }

        public void RegisterKilnRecipes(RecipeKiln recipeKilnRecipes)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register CNC recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeKilnRecipes.RecipeID = KilnRecipes.Count + 1;
            this.KilnRecipes.Add(recipeKilnRecipes);
        }

        public void RegisterCokeOvenRecipe(RecipeCokeOven recipeCokeOven)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register CNC recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeCokeOven.RecipeID = CokeOvenRecipes.Count + 1;
            this.CokeOvenRecipes.Add(recipeCokeOven);
        }

        public void RegisterAlloyOvenRecipe(RecipeAlloyOven recipeAlloyOven)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register CNC recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeAlloyOven.RecipeID = AlloyOvenRecipes.Count + 1;
            this.AlloyOvenRecipes.Add(recipeAlloyOven);
        }
    }
}
