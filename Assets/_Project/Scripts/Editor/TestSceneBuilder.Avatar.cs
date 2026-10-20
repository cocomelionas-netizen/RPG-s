using System.Collections.Generic;
using System.Linq;
using RPG.Characters;
using RPG.Items;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Heroes drawn from the gear they wear: Flare's hero layers (<see cref="FlareAvatarData"/>, packed by
    /// Tools/Art/avatar.py) become one animation set per look in the <see cref="AvatarCatalog"/>, and every piece of gear
    /// gets the look of its kind (<see cref="ItemDefinition.appearance"/>).
    /// </summary>
    public static partial class TestSceneBuilder
    {
        private const string AvatarAnimationsFolder = "Assets/_Project/Data/Characters/Avatar";
        private const string AvatarCatalogPath = "Assets/_Project/Data/Characters/AvatarCatalog.asset";

        private static AvatarCatalog CreateAvatarCatalog()
        {
            System.IO.Directory.CreateDirectory(AvatarAnimationsFolder);
            var male = new List<AvatarPart>();
            var female = new List<AvatarPart>();
            foreach (FlareAvatarData.Sheet sheet in FlareAvatarData.Sheets)
            {
                Texture2D atlas = FlareAtlas($"{FlareAvatarData.Folder}/{sheet.Atlas}.png");
                AvatarSlot slot = AvatarSlotOf(sheet.Slot);
                foreach (FlareAvatarData.Part part in sheet.Parts)
                {
                    string assetName = $"{sheet.Gender}_{part.Name}";
                    string path = $"{AvatarAnimationsFolder}/{assetName}.asset";
                    var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(path);
                    if (set == null)
                    {
                        set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
                        AssetDatabase.CreateAsset(set, path);
                    }
                    RemoveSubSprites(set);

                    // Only the frames this part's clips use, each cut once.
                    var sprites = new Dictionary<int, Sprite>();
                    Sprite Frame(int i)
                    {
                        if (sprites.TryGetValue(i, out Sprite made)) return made;
                        int[] r = sheet.Rects;
                        int at = i * 6;
                        made = AtlasSprite(set, atlas, $"{assetName}_{sprites.Count}", new Rect(r[at], r[at + 1], r[at + 2], r[at + 3]),
                            new Vector2(r[at + 4] / 1000f, r[at + 5] / 1000f), FlareAvatarData.PixelsPerUnit);
                        sprites[i] = made;
                        return made;
                    }

                    set.MirrorWestFromEast = false;
                    set.Clips = part.Clips.Select(clip =>
                    {
                        Sprite[] Frames(int direction) => clip.Directions[direction].Select(Frame).ToArray();
                        return new DirectionalClip
                        {
                            id = clip.Id,
                            framesPerSecond = clip.FramesPerSecond,
                            loop = clip.Loop,
                            east = Frames(0),
                            northEast = Frames(1),
                            north = Frames(2),
                            northWest = Frames(3),
                            west = Frames(4),
                            southWest = Frames(5),
                            south = Frames(6),
                            southEast = Frames(7),
                        };
                    }).ToArray();
                    EditorUtility.SetDirty(set);
                    (sheet.Gender == "Female" ? female : male).Add(new AvatarPart { slot = slot, name = part.Name, animations = set });
                }
            }

            var catalog = AssetDatabase.LoadAssetAtPath<AvatarCatalog>(AvatarCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AvatarCatalog>();
                AssetDatabase.CreateAsset(catalog, AvatarCatalogPath);
            }
            catalog.MaleParts = male.ToArray();
            catalog.FemaleParts = female.ToArray();
            catalog.LayerOrder = FlareAvatarData.LayerOrder
                .Select(order => new AvatarLayerOrder { slots = order.Select(AvatarSlotOf).ToArray() }).ToArray();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static AvatarSlot AvatarSlotOf(string flareLayer)
        {
            switch (flareLayer)
            {
                case "head": return AvatarSlot.Head;
                case "chest": return AvatarSlot.Chest;
                case "legs": return AvatarSlot.Legs;
                case "feet": return AvatarSlot.Feet;
                case "hands": return AvatarSlot.Hands;
                case "main": return AvatarSlot.MainHand;
                default: return AvatarSlot.OffHand;
            }
        }

        /// <summary>
        /// The Flare look of every piece of gear: armor by its kind (cloth, leather, chain, plate, robes) and slot, weapons by
        /// type and level, shields by level. Cloth has no Flare helmet, so a cloth hat leaves the hair showing; jewellery
        /// and belts are not drawn.
        /// </summary>
        private static void AssignAppearances(IEnumerable<ItemDefinition> items)
        {
            foreach (ItemDefinition item in items)
            {
                if (item == null) continue;
                item.appearance = AppearanceOf(item);
                EditorUtility.SetDirty(item);
            }
        }

        private static string AppearanceOf(ItemDefinition item)
        {
            string name = item.displayName.ToLowerInvariant();
            // A step for the level: 0 up to level 5, 1 up to 11, 2 up to 17, 3 from 18 (as the icons).
            int step = item.itemLevel <= 5 ? 0 : item.itemLevel <= 11 ? 1 : item.itemLevel <= 17 ? 2 : 3;
            switch (item.slot)
            {
                case ItemSlot.Weapon:
                    switch (item.weaponType)
                    {
                        case WeaponType.Ranged: return step == 0 ? "shortbow" : "longbow";
                        case WeaponType.Staff: return "staff";
                        case WeaponType.Wand: return "wand";
                        case WeaponType.Axe: return "battle_axe";
                        case WeaponType.Mace: return "mace";
                        case WeaponType.Dagger: return "dagger";
                        default: return step == 0 ? "shortsword" : "longsword";
                    }
                case ItemSlot.Shield: return step == 0 ? "buckler" : "kite_shield";
                case ItemSlot.Head:
                case ItemSlot.Chest:
                case ItemSlot.Legs:
                case ItemSlot.Feet:
                case ItemSlot.Hands:
                    break;
                default: return null;
            }

            int piece = item.slot == ItemSlot.Head ? 0 : item.slot == ItemSlot.Chest ? 1 : item.slot == ItemSlot.Legs ? 2
                : item.slot == ItemSlot.Feet ? 3 : 4;
            switch (ArmorKindOf(item, name))
            {
                case GearData.ArmorKind.Cloth: return new[] { null, "cloth_shirt", "cloth_pants", "cloth_sandals", "cloth_gloves" }[piece];
                case GearData.ArmorKind.Leather: return new[] { "leather_hood", "leather_chest", "leather_pants", "leather_boots", "leather_gloves" }[piece];
                case GearData.ArmorKind.Chain: return new[] { "chain_coif", "chain_cuirass", "chain_greaves", "chain_boots", "chain_gloves" }[piece];
                case GearData.ArmorKind.Plate: return new[] { "plate_helm", "plate_cuirass", "plate_greaves", "plate_boots", "plate_gauntlets" }[piece];
                default: return new[] { "mage_hood", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves" }[piece];
            }
        }
    }
}
