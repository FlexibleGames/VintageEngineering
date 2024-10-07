using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Client;
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
        /// Bake small things into other things.
        /// </summary>
        public List<RecipeKiln>         KilnRecipes = new List<RecipeKiln>();
        /// <summary>
        /// Turn Combustable things into other things
        /// </summary>
        public List<RecipeCreosoteOven>     CreosoteOvenRecipes = new List<RecipeCreosoteOven>();
        /// <summary>
        /// Smelt things into other things
        /// </summary>
        public List<RecipeAlloyOven>    AlloyOvenRecipes = new List<RecipeAlloyOven>();

        private readonly Dictionary<string, List<Block>> recipeMachines = new();

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
            AddRecipesToHandbook(api, this.MetalPressRecipes, "metalpress", "vinteng:Presses into", "vinteng:Pressing");
            this.LogSplitterRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeLogSplitter>>("velogsplitterrecipes").Recipes;
            AddRecipesToHandbook(api, this.LogSplitterRecipes, "logsplitter", "vinteng:Splits into", "vinteng:Splitting");
            this.SawMillRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeSawMill>>("vesawmillrecipes").Recipes;
            AddRecipesToHandbook(api, this.SawMillRecipes, "sawmill", "vinteng:Saws into", "vinteng:Sawing");

            this.ExtruderRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeExtruder>>("veextruderrecipes").Recipes;
            AddRecipesToHandbook(api, this.ExtruderRecipes, "extruder", "vinteng:Extrudes into", "vinteng:Extruding");

            this.CrusherRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeCrusher>>("vecrusherrecipes").Recipes;
            AddRecipesToHandbook(api, this.CrusherRecipes, "crusher", "vinteng:Industrially crushes into", "vinteng:Industrially crushing");
            this.KilnRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeKiln>>("vekilnrecipes").Recipes;
            AddRecipesToHandbook(api, this.KilnRecipes, "kiln", "vinteng:Industrially fires into", "vinteng:Industrially firing");
            this.MixerRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeMixer>>("vemixerrecipes").Recipes;
            AddRecipesToHandbook(api, this.MixerRecipes, "mixer", "vinteng:Industrially mixes into", "vinteng:Industrially mixing");
            this.CreosoteOvenRecipes = api.RegisterRecipeRegistry<RecipeRegistryGeneric<RecipeCreosoteOven>>("vecreosoteoven").Recipes;
            AddRecipesToHandbook(api, this.CreosoteOvenRecipes, "creosoteoven", "vinteng:Industrially bakes into", "vinteng:Industrially baking");
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

        public void RegisterKilnRecipe(RecipeKiln recipeKilnRecipes)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register VE recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeKilnRecipes.RecipeID = KilnRecipes.Count + 1;
            this.KilnRecipes.Add(recipeKilnRecipes);
        }

        public void RegisterCreosoteOvenRecipe(RecipeCreosoteOven recipeCreosoteOven)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register VE recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeCreosoteOven.RecipeID = CreosoteOvenRecipes.Count + 1;
            this.CreosoteOvenRecipes.Add(recipeCreosoteOven);
        }

        public void RegisterAlloyOvenRecipe(RecipeAlloyOven recipeAlloyOven)
        {
            if (!VERecipeRegistrySystem.canRegister)
            {
                throw new InvalidOperationException("VintEng | RecipeRegistrySystem: Can no longer register VE recipes. Register during AssetsLoaded/AssetsFinalize and with ExecuteOrder < 99999");
            }
            recipeAlloyOven.RecipeID = AlloyOvenRecipes.Count + 1;
            this.AlloyOvenRecipes.Add(recipeAlloyOven);
        }

        /// <summary>
        /// Register the machine as capable of handling recipes of a given type
        /// </summary>
        /// <param name="recipeType">The type of recipe the machine can handle</param>
        /// <param name="machine">The machine that processes recipes</param>
        public void RegisterRecipeMachine(string recipeType, Block machine)
        {
            if (!recipeMachines.TryGetValue(recipeType, out List<Block> blocks))
            {
                blocks = new();
                recipeMachines.Add(recipeType, blocks);
            }
            blocks.Add(machine);
        }

        /// <summary>
        /// Finds all recipe outputs that take the ingredient, ignoring stack attributes.
        /// </summary>
        /// <param name="input">the input ingredient to search for</param>
        /// <returns>A dictionary of recipe outputs. The outputs are grouped in the dictionary their recipe name.</returns>
        private static IReadOnlyDictionary<AssetLocation, List<ItemStack>>
        GetOutputsForIngredient<T>(IReadOnlyList<IVEMachineRecipeBase<T>> recipes, ItemStack input)
        {
            Dictionary<AssetLocation, List<ItemStack>> result = null;
            foreach (IVEMachineRecipeBase<T> recipe in recipes)
            {
                for (int inputIndex = 0; inputIndex < recipe.Ingredients.Length; ++inputIndex)
                {
                    if (recipe.SatisfiesAsIngredient(inputIndex, input, false))
                    {
                        for (int outputIndex = 0; outputIndex < recipe.Outputs.Length; ++outputIndex)
                        {
                            result ??= new();
                            if (!result.TryGetValue(recipe.Name,
                                                    out List<ItemStack> resultList))
                            {
                                resultList = new();
                                result.Add(recipe.Name, resultList);
                            }
                            resultList.Add(recipe.GetResolvedOutput(outputIndex));
                        }
                        break;
                    }
                }
            }
            return (IReadOnlyDictionary<AssetLocation, List<ItemStack>>)result ??
                   ImmutableDictionary<AssetLocation, List<ItemStack>>.Empty;
        }

        /// <summary>
        /// Finds the ingredients for any recipes that produces the output, ignoring stack attributes.
        /// </summary>
        /// <param name="output">the recipe output to search for</param>
        /// <param name="allStacks">every resolved item</param>
        /// <returns>A dictionary of recipe ingredients. The ingredients are grouped in the dictionary their recipe name.</returns>
        private static IReadOnlyDictionary<AssetLocation, List<ItemStack>>
        GetIngredientsForOutput<T>(ICoreClientAPI capi, IReadOnlyList<IVEMachineRecipeBase<T>> recipes, ItemStack output, ItemStack[] allStacks)
        {
            Dictionary<AssetLocation, List<ItemStack>> result = null;
            foreach (IVEMachineRecipeBase<T> recipe in recipes)
            {
                for (int outputIndex = 0; outputIndex < recipe.Outputs.Length; ++outputIndex)
                {
                    if (recipe.GetResolvedOutput(outputIndex).Equals(
                            capi.World, output, GlobalConstants.IgnoredStackAttributes))
                    {
                        for (int inputIndex = 0; inputIndex < recipe.Ingredients.Length; ++inputIndex)
                        {
                            result ??= new();
                            if (!result.TryGetValue(recipe.Name,
                                                    out List<ItemStack> resultList))
                            {
                                resultList = new();
                                result.Add(recipe.Name, resultList);
                            }
                            ItemStack resolved = recipe.GetResolvedInput(inputIndex);
                            if (resolved != null)
                            {
                                resultList.Add(recipe.GetResolvedInput(inputIndex));
                            }
                            else
                            {
                                foreach (ItemStack item in allStacks)
                                {
                                    if (recipe.SatisfiesAsIngredient(inputIndex, item, false))
                                    {
                                        resultList.Add(item);
                                    }
                                }
                            }
                        }
                        break;
                    }
                }
            }
            return (IReadOnlyDictionary<AssetLocation, List<ItemStack>>)result ??
                   ImmutableDictionary<AssetLocation, List<ItemStack>>.Empty;
        }

        private static void AddRecipeProcessesInto<T>(ICoreClientAPI capi, IReadOnlyList<IVEMachineRecipeBase<T>> recipes,
                          string processesIntoVerb,
                          ActionConsumable<string> openDetailPageFor, ItemStack stack,
                          List<RichTextComponentBase> components, ref bool haveText)
        {
            IReadOnlyDictionary<AssetLocation, List<ItemStack>> groupedOutputs = GetOutputsForIngredient(recipes, stack);
            if (groupedOutputs.Count == 0)
            {
                return;
            }
            CollectibleBehaviorHandbookTextAndExtraInfoPatch.AddHeading(components, capi, processesIntoVerb, ref haveText);

            foreach (List<ItemStack> group in groupedOutputs.Values)
            {
                SlideshowItemstackTextComponent output =
                    new(capi, group.ToArray(),
                        GuiStyle.LargeFontSize, EnumFloat.Inline,
                        (ingredient) =>
                            openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(
                                ingredient)))
                    { ShowStackSize = true };
                components.Add(output);
            };
            // Add a newline
            components.Add(new ClearFloatTextComponent(capi, CollectibleBehaviorHandbookTextAndExtraInfoPatch.MarginBottom));
        }

        private void AddRecipeCreatedBy<T>(ICoreClientAPI capi, IReadOnlyList<IVEMachineRecipeBase<T>> recipes,
                                           string requiredMachine, string createdByVerb,
                                           ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor,
                                           ItemStack stack, ref List<RichTextComponentBase> components)
        {
            IReadOnlyDictionary<AssetLocation, List<ItemStack>> groupedInputs = GetIngredientsForOutput(capi, recipes, stack, allStacks);
            if (groupedInputs.Count == 0)
            {
                return;
            }
            if (components == null)
            {
                components = new();
            }
            else
            {
                components.Add(new ClearFloatTextComponent(capi, CollectibleBehaviorHandbookTextAndExtraInfoPatch.SmallPadding));
            }
            CollectibleBehaviorHandbookTextAndExtraInfoPatch.AddSubHeading(components, capi, openDetailPageFor, createdByVerb, null);

            bool first = true;
            foreach (List<ItemStack> group in groupedInputs.Values)
            {
                if (!first)
                {
                    // Add a newline
                    components.Add(new ClearFloatTextComponent(capi, CollectibleBehaviorHandbookTextAndExtraInfoPatch.SmallPadding));
                }
                first = false;
                SlideshowItemstackTextComponent input =
                    new(capi, group.ToArray(),
                        GuiStyle.LargeFontSize, EnumFloat.Inline,
                        (ingredient) =>
                            openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(
                                ingredient)))
                    { ShowStackSize = true };
                components.Add(input);
                if (requiredMachine != null)
                {
                    RichTextComponent text = new(capi, Lang.Get("vinteng:in machine"), CairoFont.WhiteSmallText())
                    {
                        VerticalAlign = EnumVerticalAlign.Middle
                    };
                    components.Add(text);
                    ItemStack[] machineItems = Array.Empty<ItemStack>();
                    if (recipeMachines.TryGetValue(requiredMachine, out List<Block> machineBlocks))
                    {
                        machineItems = machineBlocks.Select((block) => new ItemStack(block)).ToArray();
                    }
                    SlideshowItemstackTextComponent machines =
                        new(capi, machineItems,
                            GuiStyle.LargeFontSize, EnumFloat.Inline,
                            (ingredient) =>
                                openDetailPageFor(GuiHandbookItemStackPage.PageCodeForStack(
                                    ingredient)))
                        { ShowStackSize = true };
                    components.Add(machines);
                }
            };
            // Add a newline
            components.Add(new ClearFloatTextComponent(capi, CollectibleBehaviorHandbookTextAndExtraInfoPatch.MarginBottom));
        }

        private void AddRecipesToHandbook<T>(ICoreAPI api, IReadOnlyList<IVEMachineRecipeBase<T>> recipes, string requiredMachine, string processesIntoVerb, string createdByVerb)
        {
            if (api.Side != EnumAppSide.Client)
            {
                return;
            }
            CollectibleBehaviorHandbookTextAndExtraInfoPatch.ProcessesInto +=
                delegate (ICoreClientAPI capi, ActionConsumable<string> openDetailPageFor, ItemStack stack,
                          List<RichTextComponentBase> components, ref bool haveText)
                {
                    AddRecipeProcessesInto(capi, recipes, processesIntoVerb, openDetailPageFor, stack, components, ref haveText);
                };

            CollectibleBehaviorHandbookTextAndExtraInfoPatch.CreatedBy +=
                delegate (ICoreClientAPI capi, ItemStack[] allStacks, ActionConsumable<string> openDetailPageFor,
                          ItemStack stack, ref List<RichTextComponentBase> components)
                {
                    AddRecipeCreatedBy(capi, recipes, requiredMachine, createdByVerb,
                                       allStacks, openDetailPageFor, stack, ref components);
                };
        }
    }
}
