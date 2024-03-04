using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VintageEngineering.RecipeSystem.Recipes
{
    public interface IVEMachineRecipeBase<T>
    {
        /// <summary>
        /// Name typically is the path of the file that defines the recipe.
        /// </summary>
        AssetLocation Name { get; set; }

        /// <summary>
        /// What is this recipes Code? Handy for debugging and tracking issues.
        /// </summary>
        string Code { get; set; }

        /// <summary>
        /// Recipes disabled in JSON ("enabled": false) are not loaded at runtime. <br/>
        /// Recipes can be disabled at runtime by setting this flag to false.<br/>        
        /// JSON Disabled recipes can not be enabled at runtime as they're not loaded.
        /// </summary>
        bool Enabled { get; set; }

        /// <summary>
        /// Used in EVERY machine!<br/>
        /// How much power is required for one iteration of this recipe to complete.<br/>
        /// Speed of craft will rely entirely on how much power per second (PPS) a machine operates at, 
        /// this allows for added machine tiers and machine upgrades.
        /// </summary>
        long PowerPerCraft { get; set; }

        /// <summary>
        /// What custom Attributes are available for this recipe?<br/>
        /// For example, Metal press uses a "requires" tag to set the item code of the mold to use.
        /// </summary>
        JsonObject Attributes { get; set; }

        /// <summary>
        /// This matches any wildcard * values to game codes.<br/>
        /// <u>Important:</u> when referencing items outside of the mod, use "domain:" on the item to map it to the right source.<br/>
        /// For Example: If one ingredient is any metal ingot from the base game, use "game:ingot-*" as the code.
        /// </summary>
        /// <param name="world"></param>
        /// <returns>Mapping of name to all allowed variants.</returns>
        Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world);

        /// <summary>
        /// Turns Ingredients (and Outputs) into IItemStacks<br/>
        /// Also use this to process any custom recipe Attributes!
        /// </summary>
        /// <param name="world"></param>
        /// <param name="sourceForErrorLogging"></param>
        /// <returns>True if successful</returns>
        bool Resolve(IWorldAccessor world, string sourceForErrorLogging);

        /// <summary>
        /// Creates a copy of this recipe.
        /// </summary>
        /// <returns></returns>
        T Clone();

        /// <summary>
        /// Recipe Ingredients in any order.<br/>
        /// Typically of type CraftingRecipeIngredient unless you need custom features.
        /// </summary>
        IRecipeIngredient[] Ingredients { get; }

        /// <summary>
        /// Recipe Outputs in any order.<br/>
        /// Typically of type VERecipeVariableOuput for VE variable-output recipes.
        /// </summary>
        IRecipeOutput[] Outputs { get; }
    }
}
