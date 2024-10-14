using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.RecipeSystem.Recipes
{
    /// <summary>
    /// Alloy oven makes all the alloys, naturally. Can smelt normal metals too I guess...<br/>
    /// Optional powered blowers would speed it up. <br/>
    /// 4 inputs, 1 fuel, 4 outputs
    /// </summary>
    public class RecipeBlastFurnace : IByteSerializable, IVEMachineRecipeBase<RecipeBlastFurnace>
    {
        /// <summary>
        /// Increases as recipes are added, first recipe added is ID=1, second is ID=2 and so on.
        /// </summary>
        public int RecipeID;

        /// <summary>
        /// Returns max MeltingPoint temp of all ingredients OR if no ingredient has a MeltingPoint returns Attributes['mintemp'] of the recipe.<br/>
        /// Returns 0 if no temp was defined anywhere.
        /// </summary>
        public int MinTemp
        {
            get
            {
                int temp = 0;
                for (int i = 0; i < 4; i++)
                {
                    if (Ingredients[i] != null)
                    {
                        CombustibleProperties cprops = Ingredients[i].ResolvedItemstack.Collectible.CombustibleProps;
                        if (cprops != null)
                        {
                            if (cprops.MeltingPoint > 0 && cprops.MeltingPoint > temp)
                            {
                                temp = cprops.MeltingPoint;
                            }
                        }
                    }
                }
                if (temp == 0)
                {
                    // none of the 4 ingredients have CombustableProps, use MinTemp attribute
                    temp = Attributes != null ? Attributes["mintemp"].AsInt(0) : 0;
                }
                return temp;
            }
        }

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
        /// Whether or not this recipe requires a blower on the Furnace, set in JSON Attributes['requireblowers']
        /// </summary>
        public bool RequireBlowers { get; set; }
        /// <summary>
        /// If RequireBlowers is true, the minimum (1 or 2) number of blowers this recipe requires, set in JSON Attributes['blowercount']
        /// </summary>
        public int RequireBlowerCount { get; set; }

        /// <summary>
        /// Not Used
        /// </summary>
        public bool RequiresDurability { get; set; }

        public string Code { get; set; }

        /// <summary>
        /// For this machine, this value = CraftTimePerItem (in seconds)<br/>
        /// for recipes that do not include an ingredient that defines MeltingDuration
        /// </summary>
        public long PowerPerCraft { get; set; }

        /// <summary>
        /// Craft time per input item in seconds for this recipe, set via the "powerpercraft" JSON attribute.
        /// </summary>
        public long CraftTimePerItem => PowerPerCraft;

        [JsonProperty]
        [JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Attributes { get; set; }


        public CraftingRecipeIngredient[] Ingredients;
        public VERecipeVariableOutput[] Outputs;

        IRecipeIngredient[] IVEMachineRecipeBase<RecipeBlastFurnace>.Ingredients
        {
            get
            {
                return Ingredients;
            }
        }


        IRecipeOutput[] IVEMachineRecipeBase<RecipeBlastFurnace>.Outputs
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

        /// <summary>
        /// Checks the validity of given ingredients BE state (Blowers) to recipe.<br/>        
        /// </summary>        
        /// <param name="ingredients">ItemSlot[] input ingredients</param>
        /// <param name="furnace">Furnace Entity calling the function.</param>
        /// <returns>True if valid.</returns>
        public bool Matches(ItemSlot[] ingredients, BEBlastFurnace furnace)
        {
            if (ingredients == null || furnace == null) return false; // no ingredients to even check, bounce
            int numactiveblowers = furnace.NumActiveBlowers; // grab this once so we don't recalculate it every time

            if (RequireBlowers && numactiveblowers == 0) return false;
            if (RequireBlowers && numactiveblowers < RequireBlowerCount) return false;


            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = PairInput(ingredients);
            if (matched == null) return false;

            return true;
        }

        /// <summary>
        /// Checks all inputSlots and compares to recipe Ingredients that match type.
        /// </summary>
        /// <param name="inputStacks">Input Slots to check</param>
        /// <returns>Matched Pair List</returns>
        public List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> PairInput(ItemSlot[] inputStacks)
        {
            List<CraftingRecipeIngredient> ingredientList = new List<CraftingRecipeIngredient>(this.Ingredients);
            Queue<ItemSlot> inputSlotsList = new Queue<ItemSlot>();
            foreach (ItemSlot val in inputStacks)
            {
                if (!val.Empty)
                {
                    inputSlotsList.Enqueue(val);
                }
            }
            if (inputSlotsList.Count != this.Ingredients.Length)
            {
                return null;
            }
            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>>();
            while (inputSlotsList.Count > 0)
            {
                ItemSlot inputSlot = inputSlotsList.Dequeue();
                bool found = false;
                for (int i = 0; i < ingredientList.Count; i++)
                {
                    CraftingRecipeIngredient ingred = ingredientList[i];
                    if (ingred.SatisfiesAsIngredient(inputSlot.Itemstack, true))
                    {
                        matched.Add(new KeyValuePair<ItemSlot, CraftingRecipeIngredient>(inputSlot, ingred));
                        found = true;
                        ingredientList.RemoveAt(i);
                        break;
                    }
                }
                if (!found)
                {
                    return null;
                }
            }
            if (ingredientList.Count > 0)
            {
                return null;
            }
            return matched;
        }

        public RecipeBlastFurnace Clone()
        {
            CraftingRecipeIngredient[] inclone = new CraftingRecipeIngredient[Ingredients.Length];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                inclone[i] = Ingredients[i].Clone();
            }
            VERecipeVariableOutput[] outclone = new VERecipeVariableOutput[Outputs.Length];
            for (int i = 0; i < Outputs.Length; i++)
            {
                outclone[i] = Outputs[i].Clone();
            }
            return new RecipeBlastFurnace
            {
                RecipeID = this.RecipeID,
                Name = this.Name,
                Enabled = this.Enabled,
                Requires = Requires != null ? this.Requires.Clone() : null,
                RequiresVariants = this.RequiresVariants != null ? this.RequiresVariants.FastCopy(RequiresVariants.Length) : null,
                Code = this.Code,
                PowerPerCraft = this.PowerPerCraft,
                RequireBlowers = this.RequireBlowers,
                RequireBlowerCount = this.RequireBlowerCount,
                Attributes = this.Attributes?.Clone(),
                Ingredients = inclone,
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
            bool ok = true;
            for (int i = 0; i < this.Ingredients.Length; i++)
            {
                ok &= this.Ingredients[i].Resolve(world, sourceForErrorLogging);
            }
            for (int i = 0; i < Outputs.Length; i++)
            {
                ok &= this.Outputs[i].Resolve(world, sourceForErrorLogging, true);
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
                if (Attributes["requireblowers"].Exists)
                {
                    RequireBlowers = Attributes["requireblowers"].AsBool(false);
                }
                if (RequireBlowers && Attributes["blowercount"].Exists)
                {
                    RequireBlowerCount = Attributes["blowercount"].AsInt(1);
                }
            }
            return ok;
        }
        public void FromBytes(BinaryReader reader, IWorldAccessor resolver)
        {
            RecipeID = reader.ReadInt32();
            Name = new AssetLocation(reader.ReadString());
            Code = reader.ReadBoolean() ? reader.ReadString() : null;
            PowerPerCraft = reader.ReadInt64();
            Attributes = reader.ReadBoolean() ? new JsonObject(JToken.Parse(reader.ReadString())) : null;
            Ingredients = new CraftingRecipeIngredient[reader.ReadInt32()];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new CraftingRecipeIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "VE Blast Furnace Recipe (FromBytes)");
            }
            Outputs = new VERecipeVariableOutput[reader.ReadInt32()];
            for (int i = 0; i < Outputs.Length; i++)
            {
                Outputs[i] = new VERecipeVariableOutput();
                Outputs[i].FromBytes(reader, resolver.ClassRegistry);
                Outputs[i].Resolve(resolver, "VE Blast Furnace Recipe (FromBytes)");
            }
            if (Attributes != null)
            {
                if (Attributes["requires"].Exists) Requires = new AssetLocation(Attributes["requires"].AsString());

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
                if (Attributes["requireblowers"].Exists)
                {
                    RequireBlowers = Attributes["requireblowers"].AsBool(false);
                }
                if (RequireBlowers && Attributes["blowercount"].Exists)
                {
                    RequireBlowerCount = Attributes["blowercount"].AsInt(1);
                }
            }
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(RecipeID);
            writer.Write(Name.ToShortString());
            writer.Write(Code != null);
            if (Code != null) { writer.Write(Code); }
            writer.Write(PowerPerCraft);
            writer.Write(Attributes != null);
            if (Attributes != null) { writer.Write(Attributes.Token.ToString()); }
            writer.Write(Ingredients.Length);
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i].ToBytes(writer);
            }
            writer.Write(Outputs.Length);
            for (int i = 0; i < Outputs.Length; i++)
            {
                Outputs[i].ToBytes(writer);
            }
        }
    }
}
