using System.Collections.Generic;
using RPG.Characters;
using RPG.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace RPG.EditorTools
{
    /// <summary>
    /// The source game's townsfolk (<see cref="TownsfolkData"/>): every friendly NPC of the island that is not a vendor,
    /// where it stands, with its gossip menu (what it says and the replies, <see cref="GossipCatalog"/>). NPCs and vendors
    /// are dressed in Flare's hero layers after their source model (a knight in plate, a priest in robes, peasants in
    /// cloth), standing and facing the viewer; the goblin peddlers are Flare's goblin.
    /// </summary>
    public static partial class TestSceneBuilder
    {
        private const string GossipCatalogPath = "Assets/_Project/Data/World/GossipCatalog.asset";

        /// <summary>One NPC look: man or woman, and the Flare part of each layer (null: bare).</summary>
        private struct NpcOutfit
        {
            public bool female;
            public string head, chest, legs, feet, hands, mainHand, offHand;

            public string PartFor(AvatarSlot slot)
            {
                switch (slot)
                {
                    case AvatarSlot.Head: return head;
                    case AvatarSlot.Chest: return chest;
                    case AvatarSlot.Legs: return legs;
                    case AvatarSlot.Feet: return feet;
                    case AvatarSlot.Hands: return hands;
                    case AvatarSlot.MainHand: return mainHand;
                    default: return offHand;
                }
            }
        }

        private static NpcOutfit Outfit(bool female, string head, string chest, string legs, string feet, string hands = null,
            string mainHand = null, string offHand = null) =>
            new NpcOutfit
            {
                female = female, head = head, chest = chest, legs = legs, feet = feet, hands = hands, mainHand = mainHand,
                offHand = offHand,
            };

        /// <summary>How each of the source game's NPC models (npc_models) is dressed here.</summary>
        private static NpcOutfit OutfitOf(string model)
        {
            switch (model)
            {
                case "knight":
                    return Outfit(false, "plate_helm", "plate_cuirass", "plate_greaves", "plate_boots", "plate_gauntlets", "longsword", "kite_shield");
                case "guild_man1": return Outfit(false, "hair", "leather_chest", "leather_pants", "leather_boots", "leather_gloves");
                case "guild_man2": return Outfit(false, "leather_hood", "leather_chest", "leather_pants", "leather_boots");
                case "wandering_trader1": return Outfit(false, "hair", "cloth_shirt", "leather_pants", "leather_boots");
                case "wandering_trader2": return Outfit(false, "leather_hood", "cloth_shirt", "cloth_pants", "leather_boots", "cloth_gloves");
                case "FemalePeasant1": return Outfit(true, "hair", "cloth_shirt", "cloth_pants", "cloth_sandals");
                case "FemalePeasant2": return Outfit(true, "hair", "cloth_shirt", "mage_skirt", "cloth_sandals");
                case "FemalePeasant3": return Outfit(true, "hair", "leather_chest", "cloth_pants", "leather_boots");
                case "FemalePeasant4": return Outfit(true, "hair", "mage_vest", "mage_skirt", "cloth_sandals");
                case "MalePeasant2": return Outfit(false, "hair", "leather_chest", "cloth_pants", "leather_boots");
                case "MalePeasant4": return Outfit(false, "hair", "cloth_shirt", "leather_pants", "cloth_sandals", "cloth_gloves");
                case "LuxHighPriestNpc": return Outfit(false, "mage_hood", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves", "staff");
                case "NoblemanBlue": return Outfit(false, "hair", "mage_vest", "leather_pants", "leather_boots", "cloth_gloves");
                case "NobleWomanFat1": return Outfit(true, "hair", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves");
                case "OldManBrown": return Outfit(false, "hair", "mage_vest", "cloth_pants", "cloth_sandals");
                case "OldManGrey": return Outfit(false, "mage_hood", "mage_vest", "mage_skirt", "mage_boots");
                case "OldWitch": return Outfit(true, "mage_hood", "mage_vest", "mage_skirt", "mage_boots", "mage_sleeves", "wand");
                case "farmer_pitchfork_working": return Outfit(false, "hair", "cloth_shirt", "cloth_pants", "leather_boots");
                // MalePeasant1 and anything else: a man in plain cloth.
                default: return Outfit(false, "hair", "cloth_shirt", "cloth_pants", "cloth_sandals");
            }
        }

        /// <summary>Every gossip text, reply and script step of the source game, in one asset.</summary>
        private static GossipCatalog CreateGossipCatalog()
        {
            var catalog = LoadOrCreateAsset<GossipCatalog>(GossipCatalogPath);
            catalog.texts = TownsfolkData.Texts;
            catalog.options = TownsfolkData.Options;
            catalog.scripts = TownsfolkData.Scripts;
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void PlaceTownsfolk(Grid grid, GossipCatalog gossip, AvatarCatalog avatars)
        {
            var parent = new GameObject("Townsfolk").transform;
            foreach (TownsfolkData.Person source in TownsfolkData.People)
            {
                foreach (Vector3Int cell in source.cells)
                {
                    var npc = new GameObject($"Townsperson {source.name}");
                    npc.transform.SetParent(parent, false);
                    npc.transform.position = grid.GetCellCenterWorld(cell);
                    DressNpc(npc, source.model, avatars);

                    // Clicked to talk; does not block walking.
                    var box = npc.AddComponent<BoxCollider2D>();
                    box.isTrigger = true;
                    box.size = new Vector2(0.4f, 0.7f);
                    box.offset = new Vector2(0f, 0.35f);
                    npc.AddComponent<Townsperson>().Configure(source.entry, source.name, source.title, source.gossip, gossip);
                }
            }
        }

        /// <summary>
        /// Draws an NPC after its source model: Flare's goblin for the goblin peddlers, else the hero layers of its outfit
        /// (standing, facing the viewer) sorted together at its feet. False when there was no art (nothing is drawn).
        /// </summary>
        private static bool DressNpc(GameObject npc, string model, AvatarCatalog avatars)
        {
            if (model == "peddler_goblin")
            {
                CharacterAnimationSet goblin = FlareAnimationSet("Goblin");
                if (!goblin.TryGetFrames(CharacterAnimationSet.IdleClip, Direction8.South, out _, out Sprite[] goblinFrames, out bool goblinFlip))
                    return false;
                var body = npc.AddComponent<SpriteRenderer>();
                body.sprite = goblinFrames[0];
                body.flipX = goblinFlip;
                body.spriteSortPoint = SpriteSortPoint.Pivot;
                return true;
            }
            if (avatars == null) return false;

            NpcOutfit outfit = OutfitOf(model);
            npc.AddComponent<SortingGroup>();
            AvatarSlot[] order = avatars.OrderFor(Direction8.South);
            bool drawn = false;
            for (int k = 0; k < order.Length; k++)
            {
                AvatarSlot slot = order[k];
                CharacterAnimationSet look = avatars.Find(outfit.female, slot, outfit.PartFor(slot) ?? AvatarCatalog.BareLook(slot))
                                             ?? avatars.Find(outfit.female, slot, AvatarCatalog.BareLook(slot));
                if (look == null || !look.TryGetFrames(CharacterAnimationSet.IdleClip, Direction8.South, out _, out Sprite[] frames, out bool flipX)
                    || frames.Length == 0)
                    continue;
                var layer = new GameObject(slot.ToString()).AddComponent<SpriteRenderer>();
                layer.transform.SetParent(npc.transform, false);
                layer.sprite = frames[0];
                layer.flipX = flipX;
                layer.sortingOrder = k;
                drawn = true;
            }
            return drawn;
        }
    }
}
