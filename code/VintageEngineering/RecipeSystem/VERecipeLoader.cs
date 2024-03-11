using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using VintageEngineering.RecipeSystem.Recipes;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Util;

namespace VintageEngineering.RecipeSystem
{    
    public class VERecipeLoader : ModSystem
    {
        ICoreServerAPI sapi;
        private bool classExclusiveRecipes = true;

        public override double ExecuteOrder()
        {
            return 1;
        }

        public override bool ShouldLoad(EnumAppSide forSide)
        {
            return forSide == EnumAppSide.Server;
        }

        public override void AssetsLoaded(ICoreAPI api)
        {
            sapi = api as ICoreServerAPI;
            if (sapi == null) return;
            classExclusiveRecipes = sapi.World.Config.GetBool("classExclusiveRecipes", true);

            VERecipeRegistrySystem verrs = sapi.ModLoader.GetModSystem<VERecipeRegistrySystem>(true);

            if (verrs == null) 
            {
                throw new InvalidOperationException("VintEng | RecipeLoader: Error retrieving VERecipeRegisterySystem! Cannot register VE Recipes!");
            }

            // Now for all the recipe loading...
            this.LoadRecipes<RecipeMetalPress>("ve metal press recipe", "recipes/vemetalpress", delegate (RecipeMetalPress r)
            {
                verrs.RegisterMetalPressRecipe(r);
            });

            sapi.World.Logger.StoryEvent(Lang.Get("Mysterious Forces...", Array.Empty<object>()));
        }

        public void LoadRecipes<T>(string name, string path, Action<T> RegisterMethod) where T : IVEMachineRecipeBase<T>
        {
            Dictionary<AssetLocation, JToken> many = this.sapi.Assets.GetMany<JToken>(this.sapi.Server.Logger, path, null);
            int recipeQuantity = 0;
            int quantityRegistered = 0;
            int quantityIgnored = 0;
            foreach (KeyValuePair<AssetLocation, JToken> val in many)
            {
                if (val.Value is JObject)
                {
                    this.LoadGenericRecipe<T>(name, val.Key, val.Value.ToObject<T>(val.Key.Domain, null), RegisterMethod, ref quantityRegistered, ref quantityIgnored);
                    recipeQuantity++;
                }
                if (val.Value is JArray)
                {
                    foreach (JToken token in (val.Value as JArray))
                    {
                        this.LoadGenericRecipe<T>(name, val.Key, token.ToObject<T>(val.Key.Domain, null), RegisterMethod, ref quantityRegistered, ref quantityIgnored);
                        recipeQuantity++;
                    }
                }
            }
            this.sapi.World.Logger.Event($"{quantityRegistered} {name}s loaded {((quantityIgnored > 0) ? $" {quantityIgnored} could not be resolved" : "")}");
        }
        private void LoadGenericRecipe<T>(string className, AssetLocation path, T recipe, Action<T> RegisterMethod, ref int quantityRegistered, ref int quantityIgnored) where T : IVEMachineRecipeBase<T>
        {
            if (!recipe.Enabled)
            {
                return;
            }
            if (recipe.Name == null)
            {
                recipe.Name = path;
            }
            ref T ptr = ref recipe;
            T t = default(T);
            if (t == null)
            {
                t = recipe;
                ptr = ref t;
            }
            Dictionary<string, string[]> nameToCodeMapping = ptr.GetNameToCodeMapping(this.sapi.World);
            if (nameToCodeMapping.Count > 0)
            {
                List<T> subRecipes = new List<T>();
                int qCombs = 0;
                bool first = true;
                foreach (KeyValuePair<string, string[]> val2 in nameToCodeMapping)
                {
                    if (first)
                    {
                        qCombs = val2.Value.Length;
                    }
                    else
                    {
                        qCombs *= val2.Value.Length;
                    }
                    first = false;
                }
                first = true;
                foreach (KeyValuePair<string, string[]> val3 in nameToCodeMapping)
                {
                    string variantCode = val3.Key;
                    string[] variants = val3.Value;
                    for (int i = 0; i < qCombs; i++)
                    {
                        T rec;
                        if (first)
                        {
                            subRecipes.Add(rec = recipe.Clone());
                        }
                        else
                        {
                            rec = subRecipes[i];
                        }
                        if (rec.Ingredients != null)
                        {
                            foreach (IRecipeIngredient ingred in rec.Ingredients)
                            {
                                if (ingred.Name == variantCode)
                                {
                                    ingred.Code = ingred.Code.CopyWithPath(ingred.Code.Path.Replace("*", variants[i % variants.Length]));
                                }
                            }
                        } 
                        if (rec.Outputs != null)
                        {
                            foreach (IRecipeOutput output in rec.Outputs)
                            {
                                output.FillPlaceHolder(variantCode, variants[i % variants.Length]);
                            }
                        }
                    }
                    first = false;
                }
                if (subRecipes.Count == 0)
                {
                    this.sapi.World.Logger.Warning($"VintEng: {path} file {className} make uses of wildcards, but no blocks or item matching those wildcards were found.");
                }
                using (List<T>.Enumerator enumerator2 = subRecipes.GetEnumerator())
                {
                    while (enumerator2.MoveNext())
                    {
                        T subRecipe = enumerator2.Current;
                        ref T ptr2 = ref subRecipe;
                        t = default(T);
                        if (t == null)
                        {
                            t = subRecipe;
                            ptr2 = ref t;
                        }
                        if (!ptr2.Resolve(this.sapi.World, className + " " + ((path != null) ? path.ToString() : null)))
                        {
                            quantityIgnored++;
                        }
                        else
                        {
                            RegisterMethod(subRecipe);
                            quantityRegistered++;
                        }
                    }
                    return;
                }
            }
            ref T ptr3 = ref recipe;
            t = default(T);
            if (t == null)
            {
                t = recipe;
                ptr3 = ref t;
            }
            if (!ptr3.Resolve(this.sapi.World, className + " " + ((path != null) ? path.ToString() : null)))
            {
                quantityIgnored++;
                return;
            }
            RegisterMethod(recipe);
            quantityRegistered++;
        }
    }
}
