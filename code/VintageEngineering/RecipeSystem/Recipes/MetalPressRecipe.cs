using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace VintageEngineering.RecipeSystem.Recipes
{
    /// <summary>
    /// Metal Press has one input and up to 2 outputs
    /// </summary>
    public class MetalPressRecipe : IByteSerializable, IVEMachineRecipeBase<MetalPressRecipe>
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
        public string Requires { get; set; }

        public string Code { get; set; }

        public long PowerPerCraft { get; set; }

        [JsonProperty]
        [JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Attributes { get; set; }

        public CraftingRecipeIngredient[] Ingredients;
        public VERecipeVariableOutput[] Outputs;

        IRecipeIngredient[] IVEMachineRecipeBase<MetalPressRecipe>.Ingredients
        {
            get
            {
                return Ingredients;
            }
        }


        IRecipeOutput[] IVEMachineRecipeBase<MetalPressRecipe>.Outputs
        {
            get
            {
                return Outputs;
            }
        }

        public MetalPressRecipe Clone()
        {
            CraftingRecipeIngredient[] cloned = new CraftingRecipeIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                cloned[i] = Ingredients[i].Clone();
            }
            VERecipeVariableOutput[] outclone = new VERecipeVariableOutput[Outputs.Length];
            for (int i = 0;i < Outputs.Length;i++)
            {
                outclone[i] = Outputs[i].Clone();
            }
            return new MetalPressRecipe
            {
                RecipeID = this.RecipeID,
                Name = this.Name,
                Enabled = this.Enabled,
                Requires = this.Requires,
                Code = this.Code,
                PowerPerCraft = this.PowerPerCraft,
                Attributes = this.Attributes?.Clone(),
                Ingredients = cloned,
                Outputs = outclone
            };
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
            for (int i = 0; i < this.Ingredients.Length; i++)
            {
                Ingredients[i].Resolve(world, sourceForErrorLogging);
            }
            for (int i = 0; i < this.Outputs.Length; i++)
            {
                Outputs[i].Resolve(world, sourceForErrorLogging);
            }
            if (Attributes != null && Attributes["requires"] != null)
            {
                Requires = Attributes["requires"].AsString();
            }
            return true;
        }

        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            RecipeID = reader.ReadInt32();
            Name = new AssetLocation(reader.ReadString());
            Requires = reader.ReadBoolean() ? reader.ReadString() : null;
            Code = reader.ReadBoolean() ? reader.ReadString() : null;
            PowerPerCraft = reader.ReadInt64();
            Attributes = reader.ReadBoolean() ? new JsonObject(JToken.Parse(reader.ReadString())) : null;
            Ingredients = new CraftingRecipeIngredient[reader.ReadInt32()];            
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new CraftingRecipeIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "Metal Press Recipe (FromBytes)");
            }
            Outputs = new VERecipeVariableOutput[reader.ReadInt32()];
            for (int i =0; i < Outputs.Length; i++)
            {
                Outputs[i] = new VERecipeVariableOutput();
                Outputs[i].FromBytes(reader, resolver.ClassRegistry);
                Outputs[i].Resolve(resolver, "Metal Press Recipe (FromBytes)");
            }
            if (Requires == null && Attributes != null && Attributes["requires"] != null)
            {
                Requires = Attributes["requires"].AsString();
            }
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(RecipeID);
            writer.Write(Name.ToShortString());            

            writer.Write(Requires != null);
            if (Requires != null) { writer.Write(Requires); }
            writer.Write(Code != null);
            if (Code != null) { writer.Write(Code); }

            writer.Write(PowerPerCraft);
            writer.Write(Attributes != null);
            if (Attributes != null) { writer.Write(Attributes.Token.ToString()); }

            writer.Write(Ingredients.Length);
            for (int i = 0; i< Ingredients.Length;i++)
            {
                Ingredients[i].ToBytes(writer);
            }
            writer.Write(Outputs.Length);
            for (int i =0; i< Outputs.Length;i++)
            {
                Outputs[i].ToBytes(writer);
            }
        }
    }
}
