using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace VintageEngineering.RecipeSystem.Recipes
{
    /// <summary>
    /// Essentially a 'shape copier', 1 material input, 1 'object to copy' slot, 2 outputs.<br/>
    /// Mass produce clay things! This recipe is for all the other things someone might want to use this machine for.
    /// </summary>
    public class RecipeCNC : IByteSerializable, IVEMachineRecipeBase<RecipeCNC>
    {
        /// <summary>
        /// Increases as recipes are added, first recipe added is ID=1, second is ID=2 and so on.
        /// </summary>
        public int RecipeID;

        public AssetLocation Name { get; set; }

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Set in attributes => requires, what item Code must be present for this recipe to progress?<br/>
        /// For the Metal Press, it is the specific mold placed into the press. Metal press inventory will have a special slot for this mold.
        /// </summary>
        public AssetLocation Requires { get; set; }

        /// <summary>
        /// Set in attributes => requirevariants, what variants, if any, are allowed of this type for this recipe.<br/>
        /// For example, for the metal press to make Titanium Plate, only the steel and titanium plate mold could be allowed.
        /// </summary>
        public string[] RequiresVariants { get; set; }

        /// <summary>
        /// Specifies whether the item this recipe requires consumes durability every craft.
        /// </summary>
        public bool RequiresDurability { get; set; }

        public string Code { get; set; }

        public long PowerPerCraft { get; set; }

        [JsonProperty]
        [JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Attributes { get; set; }


        public CraftingRecipeIngredient[] Ingredients;
        public VERecipeVariableOutput[] Outputs;

        IRecipeIngredient[] IVEMachineRecipeBase<RecipeCNC>.Ingredients
        {
            get
            {
                return Ingredients;
            }
        }


        IRecipeOutput[] IVEMachineRecipeBase<RecipeCNC>.Outputs
        {
            get
            {
                return Outputs;
            }
        }

        public RecipeCNC Clone()
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
        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            throw new NotImplementedException();
        }

        public void ToBytes(BinaryWriter writer)
        {
            throw new NotImplementedException();
        }
    }
}
