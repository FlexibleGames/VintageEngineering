using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Common;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace VintageEngineering.RecipeSystem.Recipes
{
    public class RecipeTemporalForge: IByteSerializable, IVEMachineRecipeBase<RecipeTemporalForge>
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

        public string Code { get; set; }

        /// <summary>
        /// For this machine, this value = CraftTimePerItem (in seconds)<br/>
        /// Total crafting time, is then This value * StackSize of input
        /// </summary>
        public long PowerPerCraft { get; set; }

        [JsonProperty]
        [JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Attributes { get; set; }

        public CraftingRecipeIngredient[] Ingredients;
        public VERecipeVariableOutput[] Outputs;

        IRecipeIngredient[] IVEMachineRecipeBase<RecipeTemporalForge>.Ingredients
        {
            get
            {
                return Ingredients;
            }
        }

        IRecipeOutput[] IVEMachineRecipeBase<RecipeTemporalForge>.Outputs
        {
            get
            {
                return Outputs;
            }
        }
        public bool SatisfiesAsIngredient(int index, ItemStack inputStack, bool checkStacksize = true)
        {
            return Ingredients[index].SatisfiesAsIngredient(inputStack, checkStacksize);
        }

        public ItemStack GetResolvedInput(int index)
        {
            return Ingredients[index].ResolvedItemStack;
        }

        public ItemStack GetResolvedOutput(int index)
        {
            return Outputs[index].ResolvedItemstack;
        }

        public bool TryCraft(ICoreAPI api, ItemSlot[] inputs, ItemSlot[] outputslots)
        {
            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> pairin = PairInput(inputs);
            if (pairin == null) return false;

            if (this.Outputs.Length > outputslots.Length) return false;
            List<KeyValuePair<ItemSlot, VERecipeVariableOutput>> pairedout = PairOutputs(outputslots);
            if (pairedout == null) return false;

            foreach (KeyValuePair<ItemSlot, CraftingRecipeIngredient> kvpairin in pairin)
            {
                if (kvpairin.Value.Quantity > 0)
                {
                    kvpairin.Key.TakeOut(kvpairin.Value.Quantity);
                }
                kvpairin.Key.MarkDirty();
            }

            foreach (KeyValuePair<ItemSlot, VERecipeVariableOutput> kvpair in pairedout)
            {
                if (kvpair.Key.Empty)
                {
                    kvpair.Key.Itemstack = kvpair.Value.ResolvedItemStack.Clone();
                    kvpair.Key.Itemstack.StackSize = kvpair.Value.VariableResolve(api.World, "TempForge VariableResolve");
                }
                else
                {
                   kvpair.Key.Itemstack.StackSize += kvpair.Value.VariableResolve(api.World, "TempForge VariableResolve"); 
                }
                kvpair.Key.MarkDirty();
            }
            return true;
        }

        /// <summary>
        /// Check whether the given itemStack is a liquid.
        /// </summary>
        /// <param name="itemStack">ItemStack to check</param>
        /// <returns>true if it is a liquid</returns>
        public bool ShouldBeInLiquidSlot(ItemStack itemStack)
        {
            if (itemStack == null) return false;
            if (itemStack.Collectible.IsLiquid()) return true;
            JsonObject itemAttributes = itemStack.ItemAttributes;
            return itemAttributes != null ? itemAttributes["waterTightContainerProps"].Exists : false;
        }

        /// <summary>
        /// Checks the validity of given ingredient and "requires" item to this recipe.<br/>        
        /// </summary>        
        /// <param name="ingredient">ItemSlot input ingredient</param>
        /// <param name="requireslot">Required Die Cast Code if aplicable.</param>
        /// <returns>True if valid.</returns>
        public bool Matches(ItemSlot[] ingredients)
        {
            if (ingredients.Length == 0) return false;

            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = PairInput(ingredients);
            if (matched == null) return false;

            return true;
        }

        /// <summary>
        /// Pairs output slots to their respective recipe Output, if slots are empty, match unmapped Outputs
        /// </summary>
        /// <param name="p_outputs"></param>
        /// <returns></returns>
        public List<KeyValuePair<ItemSlot, VERecipeVariableOutput>> PairOutputs(ItemSlot[] p_outputs)
        {
            if (p_outputs == null || p_outputs.Length == 0)
            {
                return null;
            }

            List<KeyValuePair<ItemSlot, VERecipeVariableOutput>> mapped = new();

            HashSet<int> matchedRecipeIndices = new();
            HashSet<int> usedOutputSlotIndices = new();

            // 1. First pass: Match already-filled output slots to recipe outputs by code
            for (int s = 0; s < p_outputs.Length; s++)
            {
                ItemSlot slot = p_outputs[s];
                if (slot.Empty)
                {
                    continue;
                }

                bool found = false;
                for (int i = 0; i < Outputs.Length; i++)
                {
                    if (matchedRecipeIndices.Contains(i))
                    {
                        continue;
                    }

                    if (slot.Itemstack.Collectible.Code == Outputs[i].ResolvedItemstack.Collectible.Code)
                    {
                        mapped.Add(new KeyValuePair<ItemSlot, VERecipeVariableOutput>(slot, Outputs[i]));
                        matchedRecipeIndices.Add(i);
                        usedOutputSlotIndices.Add(s);
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    // Filled slot that doesn't match any recipe output → invalid
                    return null;
                }
            }

            // 2. Second pass: Assign remaining recipe outputs to empty slots
            int nextLiquidSlotIndex = 1; // fluids typically start at slot 1

            for (int i = 0; i < Outputs.Length; i++)
            {
                if (matchedRecipeIndices.Contains(i))
                {
                    continue;
                }

                VERecipeVariableOutput recipeOut = Outputs[i];
                ItemSlot targetSlot = null;

                if (!recipeOut.ResolvedItemStack.Collectible.IsLiquid())
                {
                    // Solid output goes to slot 0
                    if (p_outputs.Length > 0 && !usedOutputSlotIndices.Contains(0))
                    {
                        targetSlot = p_outputs[0];
                        usedOutputSlotIndices.Add(0);
                    }
                }
                else
                {
                    // Liquid output - find next available liquid slot (starting from 1)
                    while (nextLiquidSlotIndex < p_outputs.Length)
                    {
                        if (!usedOutputSlotIndices.Contains(nextLiquidSlotIndex))
                        {
                            targetSlot = p_outputs[nextLiquidSlotIndex];
                            usedOutputSlotIndices.Add(nextLiquidSlotIndex);
                            nextLiquidSlotIndex++; // move to next possible slot
                            break;
                        }
                        nextLiquidSlotIndex++;
                    }
                }

                if (targetSlot == null || !targetSlot.Empty)
                {
                    return null;
                }

                mapped.Add(new KeyValuePair<ItemSlot, VERecipeVariableOutput>(targetSlot, recipeOut));
            }

            return mapped;
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

        public RecipeTemporalForge Clone()
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
            return new RecipeTemporalForge
            {
                RecipeID = this.RecipeID,
                Name = this.Name,
                Enabled = this.Enabled,
                Requires = Requires != null ? this.Requires.Clone() : null,
                RequiresVariants = this.RequiresVariants != null ? this.RequiresVariants.FastCopy(RequiresVariants.Length) : null,
                Code = this.Code,
                PowerPerCraft = this.PowerPerCraft,
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
            //int numFluidOutputs = 0;
            for (int i = 0; i < this.Ingredients.Length; i++)
            {
                ok &= this.Ingredients[i].Resolve(world, sourceForErrorLogging);
            }
            for (int i = 0; i < Outputs.Length; i++)
            {
                ok &= this.Outputs[i].Resolve(world, sourceForErrorLogging, true);
                //if (this.Outputs[i].ResolvedItemstack != null)
                //{
                //    if (this.Outputs[i].ResolvedItemstack.Collectible.MatterState == EnumMatterState.Liquid) numFluidOutputs++;
                //}
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
                Ingredients[i].Resolve(resolver, "VE Temporal Forge Recipe (FromBytes)");
            }
            Outputs = new VERecipeVariableOutput[reader.ReadInt32()];
            for (int i = 0; i < Outputs.Length; i++)
            {
                Outputs[i] = new VERecipeVariableOutput();
                Outputs[i].FromBytes(reader, resolver.ClassRegistry);
                Outputs[i].Resolve(resolver, "VE Temporal Forge Recipe (FromBytes)");
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
