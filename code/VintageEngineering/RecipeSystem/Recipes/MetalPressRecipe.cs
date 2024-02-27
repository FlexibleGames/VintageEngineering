using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;

namespace VintageEngineering.RecipeSystem.Recipes
{
    public class MetalPressRecipe : VEMachineRecipeBase, IRecipeBase<MetalPressRecipe>
    {
        public List<CraftingRecipeIngredient> _ingredients;
        public CraftingRecipeIngredient _output;


        public AssetLocation Name { get; set; }
        public bool Enabled { get; set; } = true;

        public IRecipeIngredient[] Ingredients
        {
            get
            {
                return _ingredients.ToArray();
            }
        }
        public IRecipeOutput Output
        { get { return _output; } }


        public MetalPressRecipe Clone()
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            throw new NotImplementedException();
        }

        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            throw new NotImplementedException();
        }
    }
}
