using System;
using UnityEngine;

namespace RPG.Progression
{
    /// <summary>All playable classes. Players refer to a class by its index here, so the order must not change.</summary>
    [CreateAssetMenu(menuName = "RPG/Progression/Class Catalog", fileName = "ClassCatalog")]
    public class ClassCatalog : ScriptableObject
    {
        public CharacterClassDefinition[] classes = Array.Empty<CharacterClassDefinition>();

        public int Count => classes.Length;

        /// <summary>The class at an index, or null when the index is invalid.</summary>
        public CharacterClassDefinition Get(int index)
        {
            return index >= 0 && index < classes.Length ? classes[index] : null;
        }
    }
}
