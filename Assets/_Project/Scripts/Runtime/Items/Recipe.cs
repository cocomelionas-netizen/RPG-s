using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>One material a recipe takes, and how many.</summary>
    [Serializable]
    public struct RecipeIngredient
    {
        public ItemDefinition item;
        [Min(1)] public int count;

        public RecipeIngredient(ItemDefinition item, int count)
        {
            this.item = item;
            this.count = Mathf.Max(1, count);
        }
    }

    /// <summary>
    /// A transmutation recipe (the source game's crafting_recipes: potion tiers, gems, orbs, keys): its items put into the
    /// transmuter together make <see cref="resultCount"/> of <see cref="result"/>.
    /// </summary>
    [Serializable]
    public struct Recipe
    {
        public ItemDefinition result;
        [Min(1)] public int resultCount;
        public RecipeIngredient[] ingredients;

        public bool IsValid => result != null && ingredients != null && ingredients.Length > 0;

        /// <summary>
        /// Whether the placed items make this recipe: exactly its kinds of items, at least as many of each as it takes.
        /// </summary>
        public bool Matches(IReadOnlyDictionary<ItemDefinition, int> placed)
        {
            if (!IsValid || placed == null || placed.Count != ingredients.Length) return false;
            foreach (RecipeIngredient ingredient in ingredients)
                if (ingredient.item == null || !placed.TryGetValue(ingredient.item, out int count) || count < ingredient.count) return false;
            return true;
        }

        /// <summary>The first recipe the placed items make (null when none).</summary>
        public static Recipe? Match(Recipe[] recipes, IReadOnlyDictionary<ItemDefinition, int> placed)
        {
            if (recipes == null) return null;
            foreach (Recipe recipe in recipes)
                if (recipe.Matches(placed)) return recipe;
            return null;
        }
    }
}
