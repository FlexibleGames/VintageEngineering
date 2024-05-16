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
    /// A Giant Blender... Mixer will support 4 item inputs & 2 fluid input; 1 item and 1 fluid output<br/>
    /// Will also likely try to support Barrel Recipes...<br/>
    /// More complex fluid-based crafting will exist in other machines in higher tiers.<br/>
    /// Will support Temperature-based crafting as well using Attributes "requirestemp" tag.
    /// </summary>
    public class RecipeMixer : IByteSerializable, IVEMachineRecipeBase<RecipeMixer>
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
        /// <summary>
        /// If set, what temp does the basin have to be for the recipe to progress?<br/>
        /// If this isn't set it will be ignored.
        /// </summary>
        public int RequiresTemp { get; set; }

        public string Code { get; set; }

        public long PowerPerCraft { get; set; }

        [JsonProperty]
        [JsonConverter(typeof(JsonAttributesConverter))]
        public JsonObject Attributes { get; set; }

        public BarrelRecipeIngredient[] Ingredients;
        public BarrelOutputStack[] Outputs;

        IRecipeIngredient[] IVEMachineRecipeBase<RecipeMixer>.Ingredients
        {
            get
            {
                return Ingredients;
            }
        }


        IRecipeOutput[] IVEMachineRecipeBase<RecipeMixer>.Outputs
        {
            get
            {
                return Outputs;
            }
        }

        public RecipeMixer Clone()
        {
            BarrelRecipeIngredient[] _ingredients = new BarrelRecipeIngredient[Ingredients.Length];
            for (int i = 0; i < _ingredients.Length; i++)
            {
                _ingredients[i] = Ingredients[i].Clone();
            }
            BarrelOutputStack[] _outputs = new BarrelOutputStack[Outputs.Length];
            for (int i = 0; i < Outputs.Length; i++)
            {
                _outputs[i] = Outputs[i].Clone();
            }
            return new RecipeMixer
            {
                Ingredients = _ingredients,
                Outputs = _outputs,
                Attributes = this.Attributes?.Clone(),
                PowerPerCraft = this.PowerPerCraft,
                Code = this.Code,
                Enabled = this.Enabled,
                Name = this.Name,
                RecipeID = this.RecipeID
            };
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
        /// Determines if a given set of inputslots matches this recipe.<br/>
        /// Will also return the stack sizes of the outputs of the recipe if valid.
        /// </summary>
        /// <param name="inputSlots">Set of InputSlots to check</param>
        /// <param name="outputStackMul">Output Stack Size multiplier, can be 0</param>
        /// <returns></returns>
        public bool Matches(ItemSlot[] inputSlots)
        {
            //outputStackMul = 0;
            List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> matched = PairInput(inputSlots);
            if (matched == null) return false;

            //outputStackMul = GetOutputStackSizeMultiplier(matched);
            //return outputStackMul >= 0;
            return true;
        }

        /// <summary>
        /// Tries to craft the recipe based on input slots to push into output slots.
        /// </summary>
        /// <param name="api">Api</param>
        /// <param name="inputslots">InputSlots</param>
        /// <param name="outputslots">2 Output Slots, id= 0 item, 1 fluid</param>
        /// <returns></returns>
        public bool TryCraftNow(ICoreAPI api, ItemSlot[] inputslots, ItemSlot[] outputslots)
        {
            List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> matched = PairInput(inputslots);
            if (matched == null) return false;

            ItemStack mainoutput = Outputs[0].ResolvedItemstack.Clone();
            ItemStack secondaryoutput = null;
            if (Outputs.Length > 1)
            {
                secondaryoutput = Outputs[1].ResolvedItemstack.Clone();
            }
            if (secondaryoutput != null && mainoutput.Collectible.IsLiquid() && secondaryoutput.Collectible.IsLiquid())
            {
                // if we have a second output and both are liquid, it's a bad recipe
                return false;
            }
            foreach (KeyValuePair<ItemSlot, BarrelRecipeIngredient> val in matched)
            {
                if (val.Value.ConsumeQuantity != null)
                {
                    val.Key.TakeOut(val.Value.ConsumeQuantity.Value);                    
                }
                else
                {
                    val.Key.TakeOut(val.Value.Quantity);                    
                }
            }
            foreach (BarrelOutputStack output in Outputs)
            {
                if (ShouldBeInLiquidSlot(output.ResolvedItemstack))
                {
                    output.ResolvedItemstack.StackSize = output.Quantity;
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
            foreach (ItemSlot input in inputslots)
            {
                if (input != null) input.MarkDirty();
            }
            return true;
        }

        /// <summary>
        /// Checks all inputSlots and compares to recipe Ingredients that match type.
        /// </summary>
        /// <param name="inputStacks">Input Slots to check</param>
        /// <returns>Matched Pair List</returns>
        public List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> PairInput(ItemSlot[] inputStacks)
        {
            List<BarrelRecipeIngredient> ingredientList = new List<BarrelRecipeIngredient>(this.Ingredients);
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
            List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>> matched = new List<KeyValuePair<ItemSlot, BarrelRecipeIngredient>>();
            while (inputSlotsList.Count > 0)
            {
                ItemSlot inputSlot = inputSlotsList.Dequeue();
                bool found = false;
                for (int i = 0; i < ingredientList.Count; i++)
                {
                    BarrelRecipeIngredient ingred = ingredientList[i];
                    if (ingred.SatisfiesAsIngredient(inputSlot.Itemstack, true))
                    {
                        matched.Add(new KeyValuePair<ItemSlot, BarrelRecipeIngredient>(inputSlot, ingred));
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

        public Dictionary<string, string[]> GetNameToCodeMapping(IWorldAccessor world)
        {
            Dictionary<string, string[]> mappings = new Dictionary<string, string[]>();
            if (Ingredients == null || Ingredients.Length == 0) return mappings;
            foreach (BarrelRecipeIngredient ingred in this.Ingredients)
            {
                if (ingred.Code.Path.Contains("*"))
                {
                    int wildcardStartLen = ingred.Code.Path.IndexOf("*");
                    int wildcardEndLen = ingred.Code.Path.Length - wildcardStartLen - 1;
                    List<string> codes = new List<string>();
                    if (ingred.Type == EnumItemClass.Block)
                    {
                        for (int i = 0; i < world.Blocks.Count; i++)
                        {

                            if (!(world.Blocks[i].Code == null) && !world.Blocks[i].IsMissing && WildcardUtil.Match(ingred.Code, world.Blocks[i].Code))
                            {
                                string code = world.Blocks[i].Code.Path.Substring(wildcardStartLen);
                                string codepart = code.Substring(0, code.Length - wildcardEndLen);
                                if (ingred.AllowedVariants == null || ingred.AllowedVariants.Contains(codepart))
                                {
                                    codes.Add(codepart);
                                }
                            }
                        }
                    }
                    else
                    {
                        for (int j = 0; j < world.Items.Count; j++)
                        {                            
                            if (!(world.Items[j].Code == null) && !world.Items[j].IsMissing && WildcardUtil.Match(ingred.Code, world.Items[j].Code))
                            {
                                string code2 = world.Items[j].Code.Path.Substring(wildcardStartLen);
                                string codepart2 = code2.Substring(0, code2.Length - wildcardEndLen);
                                if (ingred.AllowedVariants == null || ingred.AllowedVariants.Contains(codepart2))
                                { 
                                    codes.Add(codepart2); 
                                }
                            }
                        }
                    }
                    mappings[ingred.Name ?? ("wildcard" + mappings.Count.ToString())] = codes.ToArray();
                }
            }
            return mappings;
        }

        public bool Resolve(IWorldAccessor world, string sourceForErrorLogging)
        {
            bool ok = true;
            for (int i = 0; i < this.Ingredients.Length; i++)
            {
                BarrelRecipeIngredient ingred = this.Ingredients[i];
                bool iOk = ingred.Resolve(world, sourceForErrorLogging);
                ok = (ok && iOk);
                if (iOk)
                {
                    WaterTightContainableProps lprops = BlockLiquidContainerBase.GetContainableProps(ingred.ResolvedItemstack);
                    if (lprops != null)
                    {
                        if (ingred.Litres < 0f)
                        {
                            if (ingred.Quantity > 0)
                            {
                                world.Logger.Warning($"VintEng: VEMixer recipe {sourceForErrorLogging}, ingredient {ingred.Code} does not define a litres attribute but a quantity, will assume quantity=litres for backwards compatibility.");
                                ingred.Litres = (float)ingred.Quantity;
                                BarrelRecipeIngredient barrelRecipeIngredient = ingred;
                                int? consumeQuantity = ingred.ConsumeQuantity;
                                barrelRecipeIngredient.ConsumeLitres = ((consumeQuantity != null) ? new float?((float)consumeQuantity.GetValueOrDefault()) : null);
                            }
                            else
                            {
                                ingred.Litres = 1f;
                            }
                        }
                        ingred.Quantity = (int)(lprops.ItemsPerLitre * ingred.Litres);
                        if (ingred.ConsumeLitres != null)
                        {
                            ingred.ConsumeQuantity = new int?((int)(lprops.ItemsPerLitre * ingred.ConsumeLitres).Value);
                        }
                    }
                }
            }
            for (int i = 0; i < Outputs.Length; i++)
            {
                ok &= this.Outputs[i].Resolve(world, sourceForErrorLogging, true);
                if (ok)
                {
                    WaterTightContainableProps lprops2 = BlockLiquidContainerBase.GetContainableProps(this.Outputs[i].ResolvedItemstack);
                    if (lprops2 != null)
                    {
                        if (this.Outputs[i].Litres < 0f)
                        {
                            if (this.Outputs[i].Quantity > 0)
                            {
                                world.Logger.Warning($"VintEng: VEMixer recipe {sourceForErrorLogging}, output {this.Outputs[i].Code} does not define a litres attribute but a stacksize, will assume stacksize=litres for backwards compatibility.");
                                this.Outputs[i].Litres = (float)this.Outputs[i].Quantity;
                            }
                            else
                            {
                                this.Outputs[i].Litres = 1f;
                            }
                        }
                        this.Outputs[i].Quantity = (int)(lprops2.ItemsPerLitre * this.Outputs[i].Litres);
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
            Ingredients = new BarrelRecipeIngredient[reader.ReadInt32()];
            for (int i = 0; i < Ingredients.Length; i++)
            {
                Ingredients[i] = new BarrelRecipeIngredient();
                Ingredients[i].FromBytes(reader, resolver);
                Ingredients[i].Resolve(resolver, "VE Mixer Recipe (FromBytes)");
            }
            Outputs = new BarrelOutputStack[reader.ReadInt32()];
            for (int i = 0; i < Outputs.Length; i++)
            {
                Outputs[i] = new BarrelOutputStack();
                Outputs[i].FromBytes(reader, resolver.ClassRegistry);
                Outputs[i].Resolve(resolver, "VE Mixer Recipe (FromBytes)");
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
                if (Attributes["requirestemp"].Exists) RequiresTemp = Attributes["requirestemp"].AsInt(0);
            }
        }

        public void ToBytes(BinaryWriter writer)
        {
            writer.Write(RecipeID);
            writer.Write(Name.ToShortString());
            writer.Write(Code != null);
            if (Code != null) writer.Write(this.Code);
            writer.Write(PowerPerCraft);
            writer.Write(Attributes != null);
            if (Attributes != null) { writer.Write(Attributes.Token.ToString()); }
            writer.Write(this.Ingredients.Length);
            for (int i = 0; i < this.Ingredients.Length; i++)
            {
                this.Ingredients[i].ToBytes(writer);
            }
            writer.Write(this.Outputs.Length);
            for (int i = 0; i < this.Outputs.Length; i++)
            {
                this.Outputs[i].ToBytes(writer);
            }
        }
    }
}
