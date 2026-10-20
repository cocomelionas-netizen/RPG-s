using System;
using UnityEngine;

namespace RPG.Characters
{
    /// <summary>The layers a hero is drawn in, one sprite each (Flare's hero layers).</summary>
    public enum AvatarSlot
    {
        Head,
        Chest,
        Legs,
        Feet,
        Hands,
        MainHand,
        OffHand,
    }

    /// <summary>One look of one layer (plate helm, leather boots, longsword...) with its animations.</summary>
    [Serializable]
    public class AvatarPart
    {
        public AvatarSlot slot;
        [Tooltip("Flare's part name (plate_cuirass, longsword...); hair is the bare head, default_* the bare body.")]
        public string name;
        public CharacterAnimationSet animations;
    }

    /// <summary>The slots in drawing order (first drawn first) for one direction.</summary>
    [Serializable]
    public class AvatarLayerOrder
    {
        public AvatarSlot[] slots = Array.Empty<AvatarSlot>();
    }

    /// <summary>
    /// Every layer look of the male and the female hero, and the order the layers are drawn in per direction.
    /// All parts share the same clips and frame counts, so frame N of each lines up.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Characters/Avatar Catalog", fileName = "AvatarCatalog")]
    public class AvatarCatalog : ScriptableObject
    {
        public const string Hair = "hair";

        [SerializeField] private AvatarPart[] maleParts = Array.Empty<AvatarPart>();
        [SerializeField] private AvatarPart[] femaleParts = Array.Empty<AvatarPart>();
        [Tooltip("East, north-east, north, north-west, west, south-west, south, south-east (Direction8 order).")]
        [SerializeField] private AvatarLayerOrder[] layerOrder = Array.Empty<AvatarLayerOrder>();

        public AvatarPart[] MaleParts { get => maleParts; set => maleParts = value ?? Array.Empty<AvatarPart>(); }
        public AvatarPart[] FemaleParts { get => femaleParts; set => femaleParts = value ?? Array.Empty<AvatarPart>(); }
        public AvatarLayerOrder[] LayerOrder { get => layerOrder; set => layerOrder = value ?? Array.Empty<AvatarLayerOrder>(); }

        /// <summary>The look named <paramref name="name"/> of a slot; null when there is none.</summary>
        public CharacterAnimationSet Find(bool female, AvatarSlot slot, string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            foreach (AvatarPart part in female ? femaleParts : maleParts)
                if (part != null && part.slot == slot && part.name == name)
                    return part.animations;
            return null;
        }

        /// <summary>What a slot shows with nothing worn in it: the hair, the bare body, or nothing (hands' weapons).</summary>
        public static string BareLook(AvatarSlot slot)
        {
            switch (slot)
            {
                case AvatarSlot.Head: return Hair;
                case AvatarSlot.Chest: return "default_chest";
                case AvatarSlot.Legs: return "default_legs";
                case AvatarSlot.Feet: return "default_feet";
                case AvatarSlot.Hands: return "default_hands";
                default: return null;
            }
        }

        /// <summary>The slots drawn for <paramref name="direction"/>, first drawn first.</summary>
        public AvatarSlot[] OrderFor(Direction8 direction)
        {
            int index = (int)direction;
            return index < layerOrder.Length && layerOrder[index] != null ? layerOrder[index].slots : Array.Empty<AvatarSlot>();
        }
    }
}
