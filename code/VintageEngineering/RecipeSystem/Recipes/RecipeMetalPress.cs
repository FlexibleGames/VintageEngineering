﻿using Newtonsoft.Json;
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
    public class RecipeMetalPress : IByteSerializable, IVEMachineRecipeBase<RecipeMetalPress>
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

        IRecipeIngredient[] IVEMachineRecipeBase<RecipeMetalPress>.Ingredients
        {
            get
            {
                return Ingredients;
            }
        }


        IRecipeOutput[] IVEMachineRecipeBase<RecipeMetalPress>.Outputs
        {
            get
            {
                return Outputs;
            }
        }

        public RecipeMetalPress Clone()
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
            return new RecipeMetalPress
            {
                RecipeID = this.RecipeID,
                Name = this.Name,
                Enabled = this.Enabled,
                Requires = Requires != null ? this.Requires.Clone() : null,
                Code = this.Code,
                PowerPerCraft = this.PowerPerCraft,
                Attributes = this.Attributes?.Clone(),
                Ingredients = cloned,
                Outputs = outclone
            };
        }

        /// <summary>
        /// Checks the validity of given ingredient and requires string to this recipe.<br/>        
        /// </summary>        
        /// <param name="ingredient">ItemSlot input ingredient</param>
        /// <param name="requirescode">Required Press Mold Code if aplicable.</param>
        /// <returns></returns>
        public bool Matches(ItemSlot ingredient, ItemSlot requireslot)
        {
            if (ingredient.Empty) return false; // no ingredient to even check, bounce

            // Satisfies call ignores fields not needed to test for equality, like stacksize.
            if (!ingredient.Itemstack.Satisfies(Ingredients[0].ResolvedItemstack)) return false;     
            // check stack sizes... 
            if (ingredient.Itemstack.StackSize < Ingredients[0].ResolvedItemstack.StackSize) return false;

            if (Requires != null) // if this recipe requires something, we need to check for it in the requires slot
            {
                if (requireslot.Empty) return false;
                if (Requires.IsWildCard)
                {
                    // TODO check for variants
                    if (RequiresVariants != null)
                    {
                        return WildcardUtil.MatchesVariants(Requires, requireslot.Itemstack.Collectible.Code, RequiresVariants);
                    }
                    return WildcardUtil.Match(Requires, requireslot.Itemstack.Collectible.Code);
                }
                else
                {
                    return Requires.Equals(requireslot.Itemstack.Collectible.Code);
                }
            }
            else
            {
                return requireslot.Empty;
            }
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
            if (Attributes != null)
            {
                if (Attributes["requires"].Exists)
                {
                    Requires = new AssetLocation(Attributes["requires"].AsString());
                }
                if (Attributes["requiresvariants"].Exists)
                {
                    if (Attributes["requiresvariants"].IsArray())
                    {
                        RequiresVariants = Attributes["requiresvariants"].AsArray<string>();
                    }
                    else
                    {
                        RequiresVariants = new string[1] { Attributes["requiresvariants"].AsString() }; 
                    }
                }
                if (Attributes["requiresdurability"].Exists)
                {
                    RequiresDurability = Attributes["requiresdurability"].AsBool(false);
                }
            }

            return true;
        }

        /// <summary>
        /// Converts a Byte stream into this recipe, used for syncing client and server.
        /// </summary>
        /// <param name="reader"></param>
        /// <param name="resolver"></param>
        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            RecipeID = reader.ReadInt32();
            Name = new AssetLocation(reader.ReadString());
            Requires = reader.ReadBoolean() ? new AssetLocation(reader.ReadString()) : null;

            if (reader.ReadBoolean()) // RequireVariants
            {                
                int numvar = reader.ReadInt32();
                RequiresVariants = new string[numvar];
                for (int i =0; i<numvar; i++)
                {
                    RequiresVariants[i] = reader.ReadString();
                }
            }
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
            if (Requires == null && Attributes != null && Attributes["requires"].Exists)
            {
                Requires = new AssetLocation(Attributes["requires"].AsString());

                if (RequiresVariants == null && Attributes["requiresvariants"].Exists)
                {
                    if (Attributes["requirevariants"].IsArray())
                    {
                        RequiresVariants = Attributes["requirevariants"].AsArray<string>();
                    }
                    else
                    {
                        RequiresVariants = new string[1] { Attributes["requirevariants"].AsString() };
                    }
                }
            }
        }

        /// <summary>
        /// Convert this recipe into a byte stream to sync client and server.
        /// </summary>
        /// <param name="writer"></param>
        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(RecipeID);
            writer.Write(Name.ToShortString());            

            writer.Write(Requires != null);
            if (Requires != null) { writer.Write(Requires.ToString()); }

            writer.Write(RequiresVariants != null);
            if (RequiresVariants != null)
            {
                writer.Write(RequiresVariants.Length);
                for (int i = 0; i < RequiresVariants.Length; i++)
                {
                    writer.Write(RequiresVariants[i]);
                }
            }

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