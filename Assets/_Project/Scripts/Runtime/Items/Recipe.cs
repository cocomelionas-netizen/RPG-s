using System;
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
    /// A crafting recipe that is not a piece of gear (the source game's crafting_recipes: potion tiers, orbs, keys):
    /// its materials make <see cref="resultCount"/> of <see cref="result"/>.
    /// </summary>
    [Serializable]
    public struct Recipe
    {
        public ItemDefinition result;
        [Min(1)] public int resultCount;
        public RecipeIngredient[] ingredients;

        public bool IsValid => result != null && ingredients != null && ingredients.Length > 0;
    }
}
