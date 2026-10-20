using System;
using System.Collections.Generic;
using RPG.Items;
using RPG.Progression;
using RPG.UI;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Item and spell icons: cut from the icon atlas (Tools/Art/icons.py, IconArtData) into Resources/Icons.asset and
    /// given to every item by its kind (weapon type, armor kind and slot, gem, potion tier, junk by name) and to every
    /// spell by name.
    /// </summary>
    public static partial class TestSceneBuilder
    {
        private const string IconSetPath = "Assets/_Project/Resources/Icons.asset";

        private static readonly Dictionary<string, Sprite> IconSprites = new Dictionary<string, Sprite>();

        private static void LoadIcons()
        {
            IconSprites.Clear();
            Texture2D atlas = FlareAtlas(IconArtData.Atlas);
            var set = LoadOrCreateAsset<IconSet>(IconSetPath);
            RemoveSubSprites(set);
            var all = new List<Sprite>();
            foreach (var entry in IconArtData.Icons)
            {
                Sprite sprite = AtlasSprite(set, atlas, entry.name.Replace('/', '_'),
                    new Rect(entry.x, entry.y, IconArtData.Size, IconArtData.Size), new Vector2(0.5f, 0.5f), IconArtData.Size);
                IconSprites[entry.name] = sprite;
                all.Add(sprite);
            }
            set.all = all.ToArray();
            set.attack = Icon("flare/1");
            EditorUtility.SetDirty(set);
        }

        private static Sprite Icon(string name)
        {
            if (IconSprites.TryGetValue(name, out Sprite sprite)) return sprite;
            Debug.LogWarning($"[TestScene] Missing icon {name} (run Tools/Art/icons.py).");
            return null;
        }

        private static Sprite Flare(int index) => Icon($"flare/{index}");

        // ---------- Spells ----------

        /// <summary>Each spell's Painterly icon by its file name (TestSceneBuilder.Spells.cs).</summary>
        private static readonly Dictionary<string, string> SpellIcons = new Dictionary<string, string>
        {
            // Paladin
            { "MightyBlow", "slice-orange-2" }, { "HolyWrath", "light-royal-3" }, { "Radiance", "light-air-fire-3" },
            { "HammerOfMight", "rock-orange-3" }, { "Charge", "haste-fire-2" }, { "BlessingOfChampions", "enchant-orange-3" },
            { "DivineStrike", "beam-royal-3" }, { "Cleanse", "light-sky-2" }, { "VengeanceAura", "runes-orange-3" },
            { "BlessingOfDefense", "protect-orange-3" }, { "TouchOfSalvation", "heal-royal-3" }, { "SalvationAura", "heal-jade-3" },
            { "Retribution", "explosion-orange-2" }, { "RighteousStorm", "lighting-royal-3" }, { "FortificationAura", "protect-sky-3" },
            { "WingsOfFreedom", "haste-royal-3" }, { "AegisOfValor", "protect-red-3" }, { "Redemption", "link-spirit-3" },
            { "ShieldBlock", "shielding-spirit-3" }, { "DivineProtection", "protect-jade-3" },
            // Ranger
            { "SinisterStrike", "slice-spirit-2" }, { "EntanglingShot", "vines-plain-2" }, { "MarkTarget", "evil-eye-red-2" },
            { "MultiShot", "needles-sky-2" }, { "StunningShot", "needles-royal-2" }, { "FocusedEvasion", "wind-sky-2" },
            { "Blindside", "slice-acid-2" }, { "KidneyShot", "rip-sky-2" }, { "Vanish", "fog-sky-2" },
            { "Sabotage", "explosion-red-1" }, { "AimedShot", "needles-fire-2" }, { "Devotion", "enchant-jade-3" },
            { "SleepArrow", "fire-arrows-royal-2" }, { "Hamstring", "rip-acid-2" }, { "TacticalRetreat", "wind-grasp-air-2" },
            { "EtherealSting", "fire-arrows-jade-2" }, { "FlameArrow", "fire-arrows-2" }, { "ExplosiveArrow", "explosion-orange-3" },
            { "EyeGouge", "evil-eye-eerie-2" }, { "ArrowFlurry", "fire-arrows-sky-3" },
            // Mage
            { "Fireball", "fireball-red-2" }, { "Ignite", "fog-orange-2" }, { "ChainsOfIce", "ice-sky-1" },
            { "Scorch", "beam-red-2" }, { "IceShard", "ice-blue-2" }, { "BoonOfClairvoyance", "light-blue-2" },
            { "IceBlast", "explosion-sky-3" }, { "VimOfWisdom", "enchant-blue-3" }, { "BindSpirit", "horror-eerie-2" },
            { "BoonOfProtection", "protect-blue-3" }, { "Warmth", "light-air-fire-2" }, { "Teleport", "air-burst-sky-2" },
            { "DeepFreeze", "ice-sky-3" }, { "MagicalAmplification", "runes-blue-3" }, { "WisdomOfLazarus", "link-blue-3" },
            { "IllusionGate", "air-burst-magenta-3" }, { "RemoveCurse", "heal-sky-3" }, { "MagicalDampening", "runes-magenta-2" },
            { "Antimagic", "beam-magenta-2" }, { "Inferno", "wind-red-3" },
        };

        private static void AssignSpellIcons(IEnumerable<AbilityDefinition> spells)
        {
            foreach (AbilityDefinition spell in spells)
            {
                if (spell == null) continue;
                spell.icon = SpellIcons.TryGetValue(spell.name, out string icon) ? Icon($"spell/{icon}") : null;
                if (spell.icon == null) Debug.LogWarning($"[TestScene] No icon for the spell {spell.name}.");
                EditorUtility.SetDirty(spell);
            }
        }

        // ---------- Items ----------

        /// <summary>Junk, materials and food by a word of their name (first match wins): game-icons.net glyphs.</summary>
        private static readonly (string word, string glyph)[] GlyphWords =
        {
            ("ingot", "lorc/metal-bar"), (" log", "delapouite/log"), (" hide", "delapouite/animal-hide"), (" cloth", "delapouite/rolled-cloth"),
            ("fossil", "lorc/ammonite-fossil"), ("hair", "delapouite/hair-strands"), ("animal leg", "lorc/chicken-leg"),
            ("animal bone", "lorc/broken-bone"), ("insect claw", "lorc/crab-claw"), ("skeleton bone", "lorc/crossed-bones"),
            ("broken sword", "delapouite/rusty-sword"), ("tooth", "lorc/tooth"), ("chunk of metal", "delapouite/melting-metal"),
            ("metal", "lorc/metal-scales"), ("jewelry", "lorc/gem-necklace"), ("merchandise", "delapouite/wooden-crate"),
            ("skull", "lorc/skull-crack"), ("talon", "lorc/bird-claw"), ("hoof", "lorc/hoof"), ("insect arm", "delapouite/praying-mantis"),
            ("insect leg", "delapouite/praying-mantis"), ("insect scale", "lorc/dorsal-scales"), ("small broken claw", "delapouite/wolverine-claws"),
            ("claw", "delapouite/claws"), ("large fang", "skoll/fangs"), ("fang", "lorc/pretty-fangs"), ("shiny mineral", "lorc/crystal-cluster"),
            ("strange mineral", "lorc/crystal-growth"), ("mineral", "faithtoken/minerals"), ("shiny gem", "lorc/gems"),
            ("fur", "delapouite/animal-hide"), ("coin", "lorc/crown-coin"), ("ear", "delapouite/elf-ear"), ("tusk", "delapouite/ivory-tusks"),
            ("shard", "lorc/shard-sword"),
        };

        /// <summary>Food by a word of its name.</summary>
        private static readonly (string word, string glyph)[] FoodWords =
        {
            ("ration", "delapouite/bread"), ("pie", "lorc/pie-slice"), ("sandwich", "delapouite/sandwich"),
            ("turkey", "lorc/roast-chicken"), ("pork", "lorc/meat"),
        };

        private static void AssignItemIcons(IEnumerable<ItemDefinition> items)
        {
            foreach (ItemDefinition item in items)
            {
                if (item == null) continue;
                item.iconTint = Color.white;
                item.icon = ItemIcon(item);
                if (item.icon == null) Debug.LogWarning($"[TestScene] No icon for the item {item.displayName}.");
                EditorUtility.SetDirty(item);
            }
        }

        private static Sprite ItemIcon(ItemDefinition item)
        {
            string name = item.displayName.ToLowerInvariant();
            // A step for the level: 0 up to level 5, 1 up to 11, 2 up to 17, 3 from 18.
            int step = item.itemLevel <= 5 ? 0 : item.itemLevel <= 11 ? 1 : item.itemLevel <= 17 ? 2 : 3;

            if (item.isGem || name.Contains("dale gem"))
            {
                if (name.Contains("ruby")) return Flare(70);
                if (name.Contains("sapphire")) return Flare(68);
                if (name.Contains("emerald")) return Flare(69);
                item.iconTint = name.Contains("topaz") ? new Color(1f, 0.78f, 0.3f)
                    : name.Contains("amethyst") ? new Color(0.8f, 0.55f, 1f)
                    : new Color(0.6f, 0.9f, 1f);
                return Flare(71);
            }
            switch (item.use)
            {
                case ItemUse.Heal:
                    return Flare(name.Contains("serum") ? 85 : name.Contains("minor") || name.Contains("lesser") ? 64 : 84);
                case ItemUse.RestoreMana:
                    return Flare(name.Contains("serum") ? 87 : name.Contains("minor") || name.Contains("lesser") ? 65 : 86);
                case ItemUse.LearnSpell:
                    return Flare(225);
                case ItemUse.Return:
                    return Flare(224);
                case ItemUse.Food:
                    foreach ((string word, string glyph) in FoodWords)
                        if (name.Contains(word)) return Icon($"glyph/{glyph}");
                    return Icon("glyph/delapouite/bread");
            }
            if (item.IsEquippable) return GearIcon(item, name, step);

            if (name.Contains("key")) return Flare(226);
            if (name.Contains("treasury")) return Flare(88);
            if (name.Contains("orb"))
            {
                bool divine = name.Contains("divine");
                return Icon(name.Contains("weapon") ? (divine ? "spell/light-royal-2" : "spell/light-air-fire-2")
                    : name.Contains("armor") ? (divine ? "spell/light-jade-3" : "spell/light-blue-3")
                    : divine ? "spell/light-magenta-2" : "spell/light-jade-2");
            }
            if (name.Contains("essence")) return Icon("spell/light-magenta-2");
            if (name.Contains("dust")) return Icon("emp/2");
            foreach ((string word, string glyph) in GlyphWords)
                if (name.Contains(word)) return Icon($"glyph/{glyph}");
            return null;
        }

        private static Sprite GearIcon(ItemDefinition item, string name, int step)
        {
            switch (item.slot)
            {
                case ItemSlot.Weapon:
                    switch (item.weaponType)
                    {
                        case WeaponType.Ranged: return Flare(new[] { 113, 114, 114, 115 }[step]);
                        case WeaponType.Staff:
                        case WeaponType.Wand: return Flare(new[] { 104, 106, 105, 107 }[step]);
                        case WeaponType.Axe: return Flare(new[] { 117, 118, 119, 119 }[step]);
                        case WeaponType.Mace: return Flare(new[] { 101, 102, 111, 103 }[step]);
                        case WeaponType.Dagger: return Flare(96);
                        default: return Flare(name.Contains("rusty") ? 97 : new[] { 97, 98, 100, 99 }[step]);
                    }
                case ItemSlot.Shield: return Flare(120 + step);
                case ItemSlot.Neck: return Flare(212 + step);
                case ItemSlot.Ring: return Flare(198 + Mathf.Clamp((item.itemLevel - 1) * 10 / 25, 0, 9));
                case ItemSlot.Waist: return Flare(208);
            }

            // Armor: the Flare set of its kind, the piece of its slot (head, chest, hands, legs, feet).
            int[] set;
            switch (ArmorKindOf(item, name))
            {
                case GearData.ArmorKind.Cloth: set = new[] { 136, 129, 130, 131, 132 }; break;
                case GearData.ArmorKind.Leather: set = step == 0 ? new[] { 136, 137, 138, 139, 140 } : new[] { 144, 145, 146, 147, 148 }; break;
                case GearData.ArmorKind.Chain: set = new[] { 152, 153, 154, 155, 156 }; break;
                case GearData.ArmorKind.Plate: set = new[] { 160, 161, 162, 163, 164 }; break;
                default: set = step < 2 ? new[] { 184, 185, 186, 187, 188 } : new[] { 192, 193, 194, 195, 196 }; break;
            }
            switch (item.slot)
            {
                case ItemSlot.Head: return Flare(set[0]);
                case ItemSlot.Chest: return Flare(set[1]);
                case ItemSlot.Hands: return Flare(set[2]);
                case ItemSlot.Legs: return Flare(set[3]);
                case ItemSlot.Feet: return Flare(name.Contains("ranger") ? 176 : set[4]);
                default: return null;
            }
        }

        /// <summary>The armor kind of a piece: the source gear's own, or from the name of a hand-made one.</summary>
        private static GearData.ArmorKind ArmorKindOf(ItemDefinition item, string name)
        {
            if (GearArmorKinds.TryGetValue(item, out GearData.ArmorKind kind)) return kind;
            bool Has(params string[] words) => Array.Exists(words, word => name.Contains(word));
            if (Has("robe", "silk", "slippers", "vestments")) return GearData.ArmorKind.Robe;
            if (Has("chain", "coif")) return GearData.ArmorKind.Chain;
            if (Has("steel", "iron", "plate", "helm", "sabatons")) return GearData.ArmorKind.Plate;
            if (Has("padded", "shirt")) return GearData.ArmorKind.Cloth;
            return GearData.ArmorKind.Leather;
        }
    }
}
