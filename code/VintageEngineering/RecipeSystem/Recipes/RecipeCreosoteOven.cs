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
    /// A very slow way of making coal coke or charcoal and Creosote (a useful fluid).<br/>
    /// 2 item input, 1 item & 1 fluid output, 1 slot for a bucket under output fluid bar.
    /// </summary>
    public class RecipeCreosoteOven : IByteSerializable, IVEMachineRecipeBase<RecipeCreosoteOven>
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

        /// <summary>
        /// Craft time per input item in seconds for this recipe, set via the "powerpercraft" JSON attribute.
        /// </summary>
        public long CraftTimePerItem => PowerPerCraft;
        /// <summary>
        /// What is the minimum temperature required to start the craft.<br/>
        /// Machine Temperature gets reduced every time something is added to the input stack.
        /// </summary>
        public int MinTemp { get; set; }
        /// <summary>
        /// (Currently Unused) What is the maximum temp for this recipe, beyond which the craft stops or changes to 
        /// something that supports the higher temps (if recipe exists).<br/>
        /// Not supported at this time as there is no way to manage the temp of the oven.
        /// </summary>
        public int MaxTemp { get; set; }

        [JsonProperty]
        [JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Attributes { get; set; }


        public CraftingRecipeIngredient[] Ingredients;
        public VERecipeVariableOutput[] Outputs;

        IRecipeIngredient[] IVEMachineRecipeBase<RecipeCreosoteOven>.Ingredients
        {
            get
            {
                return Ingredients;
            }
        }


        IRecipeOutput[] IVEMachineRecipeBase<RecipeCreosoteOven>.Outputs
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
        /// Check whether the given itemStack is a liquid.
        /// </summary>
        /// <param name="itemStack">ItemStack to check</param>
        /// <returns>true if it is a liquid</returns>
        public bool ShouldBeInLiquidSlot(ItemStack itemStack)
        {
            if (itemStack == null) return false;
            JsonObject itemAttributes = itemStack.ItemAttributes;
            return itemAttributes != null ? itemAttributes["waterTightContainerProps"].Exists : false;
        }

        /// <summary>
        /// Checks the validity of given ingredient and "requires" item to this recipe.<br/>        
        /// </summary>        
        /// <param name="ingredient">ItemSlot input ingredient</param>
        /// <param name="requireslot">Required Die Cast Code if aplicable.</param>
        /// <returns>True if valid.</returns>
        public bool Matches(ItemSlot ingredient, ItemSlot requireslot)
        {
            if (ingredient.Empty) return false; // no ingredient to even check, bounce

            if (!Ingredients[0].SatisfiesAsIngredient(ingredient.Itemstack, true)) return false;

            if (Requires != null) // unused, but left in... if this recipe requires something, we need to check for it in the requires slot
            {
                if (requireslot == null || requireslot.Empty) return false;
                if (Requires.IsWildCard)
                {
                    // TODO check for variants
                    if (RequiresVariants != null)
                    {
                        return WildcardUtil.Match(Requires, requireslot.Itemstack.Collectible.Code, RequiresVariants);
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
                return true;
            }
        }


        /// <summary>
        /// Tries to craft the recipe based on input slots to push into output slots.
        /// </summary>
        /// <param name="api">Api</param>
        /// <param name="inputslots">InputSlots</param>
        /// <param name="outputslots">2 Output Slots, id= 0 item, 1 fluid</param>
        /// <returns>True if craft happened</returns>
        public bool TryCraftNow(ICoreAPI api, ItemSlot[] inputslots, ItemSlot[] outputslots)
        {
            List<KeyValuePair<ItemSlot, CraftingRecipeIngredient>> matched = PairInput(inputslots);
            if (matched == null) return false;

            ItemStack mainoutput = Outputs[0].ResolvedItemstack.Clone();
            ItemStack secondaryoutput = null;
            WaterTightContainableProps wprops = null;
            int outputslotidmain = 0;
            int outputslotidsecondary = 1;
            if (mainoutput.Collectible.IsLiquid())
            {
                // outputslots[1] is target if a fluid
                wprops = BlockLiquidContainerBase.GetContainableProps(mainoutput);
                outputslotidmain = 1;
                outputslotidsecondary = 0; // secondary output might not exist, but if it does it has to be an item
            }
            if (Outputs.Length > 1)
            {
                secondaryoutput = Outputs[1].ResolvedItemstack.Clone();
            }
            if (secondaryoutput != null && mainoutput.Collectible.IsLiquid() && secondaryoutput.Collectible.IsLiquid())
            {
                // if we have a second output and both are liquid, bad recipe, bounce.
                return false;
            }
            if (secondaryoutput != null && secondaryoutput.Collectible.IsLiquid())
            {
                // we have two outputs, and secondary is the liquid
                wprops = BlockLiquidContainerBase.GetContainableProps(secondaryoutput);
            }
            if (secondaryoutput != null && !mainoutput.Collectible.IsLiquid() && !secondaryoutput.Collectible.IsLiquid())
            { 
                // two outputs but neither is a liquid, bad recipe, bounce.
                return false; 
            }
            while (!inputslots[0].Empty)
            {
                if ((!outputslots[0].Empty && outputslots[0].Itemstack.Collectible.MaxStackSize - outputslots[0].Itemstack.StackSize > 0)
                   || (!outputslots[1].Empty && outputslots[1].Itemstack.Collectible.MaxStackSize - outputslots[1].Itemstack.StackSize > 0) )
                {
                    // if either output is full, stop crafting
                    break; 
                }            
                foreach (KeyValuePair<ItemSlot, CraftingRecipeIngredient> val in matched)
                {
                    val.Key.TakeOut(val.Value.Quantity);
                    val.Key.MarkDirty();
                }
                foreach (VERecipeVariableOutput output in Outputs)
                {
                    int quantity = output.VariableResolve(api.World, "CreosoteOven TryCraftNow");
                    output.ResolvedItemstack.StackSize = quantity;
                    if (ShouldBeInLiquidSlot(output.ResolvedItemstack))
                    {
                        if (outputslots[1].Empty) outputslots[1].Itemstack = output.ResolvedItemstack.Clone();
                        else outputslots[1].Itemstack.StackSize += output.ResolvedItemstack.StackSize;
                        outputslots[1].MarkDirty();
                    }
                    else
                    {
                        if (outputslots[0].Empty) outputslots[0].Itemstack = output.ResolvedItemstack.Clone();
                        else outputslots[0].Itemstack.StackSize += output.ResolvedItemstack.StackSize;
                        outputslots[0].MarkDirty();
                    }
                }
            }
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

        public RecipeCreosoteOven Clone()
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
            return new RecipeCreosoteOven
            {
                RecipeID = this.RecipeID,
                Name = this.Name,
                Enabled = this.Enabled,
                Requires = Requires != null ? this.Requires.Clone() : null,
                RequiresVariants = this.RequiresVariants != null ? this.RequiresVariants.FastCopy(RequiresVariants.Length) : null,
                MinTemp = this.MinTemp,
                MaxTemp = this.MaxTemp,
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
                if (Attributes["mintemp"].Exists) MinTemp = Attributes["mintemp"].AsInt(0);
                if (Attributes["maxtemp"].Exists) MaxTemp = Attributes["maxtemp"].AsInt(0);
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
                Ingredients[i].Resolve(resolver, "VE Creosote Oven Recipe (FromBytes)");
            }
            Outputs = new VERecipeVariableOutput[reader.ReadInt32()];
            for (int i = 0; i < Outputs.Length; i++)
            {
                Outputs[i] = new VERecipeVariableOutput();
                Outputs[i].FromBytes(reader, resolver.ClassRegistry);
                Outputs[i].Resolve(resolver, "VE Creosote Oven Recipe (FromBytes)");
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
                if (Attributes["mintemp"].Exists) MinTemp = Attributes["mintemp"].AsInt(0);
                if (Attributes["maxtemp"].Exists) MaxTemp = Attributes["maxtemp"].AsInt(0);
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
