using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace VintageEngineering.RecipeSystem.Recipes
{
    /// <summary>
    /// Alloy oven makes all the alloys, naturally. Can smelt normal metals too I guess...<br/>
    /// Optional powered blowers would speed it up. <br/>
    /// 4 inputs, 1 fuel, 4 outputs
    /// </summary>
    public class RecipeAlloyOven : IByteSerializable, IVEMachineRecipeBase<RecipeAlloyOven>
    {
        /// <summary>
        /// Increases as recipes are added, first recipe added is ID=1, second is ID=2 and so on.
        /// </summary>
        public int RecipeID;

        public AssetLocation Name { get; set; }

        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Set in attributes => requires, what item Code must be present for this recipe to progress?<br/>
        /// Alloy Oven does not currently use this
        /// </summary>
        public AssetLocation Requires { get; set; }

        /// <summary>
        /// Set in attributes => requirevariants, what variants, if any, are allowed of this type for this recipe.<br/>
        /// Alloy Oven doesn't use this.
        /// </summary>
        public string[] RequiresVariants { get; set; }

        /// <summary>
        /// Not Used
        /// </summary>
        public bool RequiresDurability { get; set; }

        public string Code { get; set; }

        public long PowerPerCraft { get; set; }

        [JsonProperty]
        [JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Attributes { get; set; }


        public CraftingRecipeIngredient[] Ingredients;
        public VERecipeVariableOutput[] Outputs;

        IRecipeIngredient[] IVEMachineRecipeBase<RecipeAlloyOven>.Ingredients
        {
            get
            {
                return Ingredients;
            }
        }


        IRecipeOutput[] IVEMachineRecipeBase<RecipeAlloyOven>.Outputs
        {
            get
            {
                return Outputs;
            }
        }

        public bool SatisfiesAsIngredient(int index, ItemStack inputStack, bool checkStacksize = true) {
            return Ingredients[index].SatisfiesAsIngredient(inputStack, checkStacksize);
        }

        public ItemStack GetResolvedInput(int index) {
            return Ingredients[index].ResolvedItemstack;
        }

        public ItemStack GetResolvedOutput(int index) {
            return Outputs[index].ResolvedItemstack;
        }

        public RecipeAlloyOven Clone()
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();
            foreach (CraftingRecipeIngredient val in this.Ingredients)
            {
                if (val.Name != null && val.Name.Length != 0 && val.Code.Path.Contains("*"))
                {
                    int wildcardStartLen = val.Code.Path.IndexOf("*");
                    int wildcardEndLen = val.Code.Path.Length - wildcardStartLen - 1;
                    List<string> codes = new List<string>();
                    if (val.Type == EnumItemClass.Block)
                    {
                        for (int i = 0; i < world.Blocks.Count; i++)
                        {
                            Block block = world.Blocks[i];
                            if (!(((block != null) ? block.Code : null) == null) && !block.IsMissing && (val.SkipVariants == null || !WildcardUtil.MatchesVariants(val.Code, block.Code, val.SkipVariants)) && WildcardUtil.Match(val.Code, block.Code, val.AllowedVariants))
                            {
                                string code = block.Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                codes.Add(codepart);
                            }
                        }
                    }
                    else
                    {
                        for (int j = 0; j < world.Items.Count; j++)
                        {
                            Item item = world.Items[j];
                            if (!(((item != null) ? item.Code : null) == null) && !item.IsMissing && (val.SkipVariants == null || !WildcardUtil.MatchesVariants(val.Code, item.Code, val.SkipVariants)) && WildcardUtil.Match(val.Code, item.Code, val.AllowedVariants))
                            {
                                string code2 = item.Code.Path.Substring(wildcardStartLen);
                                string codepart2 = code2.Substring(0, code2.Length - wildcardEndLen);
                                codes.Add(codepart2);
                            }
                        }
                    }
                    mappings[val.Name] = codes.ToArray();
                }
            }
            return mappings;
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
