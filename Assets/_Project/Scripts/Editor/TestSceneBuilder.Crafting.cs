using System;
using System.Collections.Generic;
using RPG.Items;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>The source game's crafting recipes (RecipeData) that this project has every item of, for the transmuter.</summary>
    public static partial class TestSceneBuilder
    {
        /// <summary>
        /// The recipes of <see cref="RecipeData"/> whose result and items are all in the catalog (by name): potion
        /// tiers and serums, gem tiers, Divine orbs, the Demiplane Key. The transmuter makes them.
        /// </summary>
        private static Recipe[] CreateRecipes(ItemCatalog catalog)
        {
            var byName = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition item in catalog.items)
                if (item != null && !item.IsEquippable && !byName.ContainsKey(item.displayName)) byName[item.displayName] = item;

            var recipes = new List<Recipe>();
            foreach (var data in RecipeData.All)
            {
                if (!byName.TryGetValue(data.result, out ItemDefinition result)) continue;
                var ingredients = new List<RecipeIngredient>();
                foreach (var ingredient in data.ingredients)
                {
                    if (!byName.TryGetValue(ingredient.item, out ItemDefinition item)) break;
                    ingredients.Add(new RecipeIngredient(item, ingredient.count));
                }
                if (ingredients.Count != data.ingredients.Length) continue;
                recipes.Add(new Recipe { result = result, resultCount = Mathf.Max(1, data.count), ingredients = ingredients.ToArray() });
            }
            Debug.Log($"[TestScene] {recipes.Count} of {RecipeData.All.Length} crafting recipes have all their items.");
            return recipes.ToArray();
        }
    }
}
