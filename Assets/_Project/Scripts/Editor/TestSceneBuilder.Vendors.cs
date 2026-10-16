using System;
using System.Collections.Generic;
using System.Linq;
using RPG.Items;
using RPG.Vendors;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// The source game's vendors (<see cref="VendorData"/>): every vendor NPC of the region where it stands, with its name,
    /// subname and stock. Shopkeepers sell their npc_vendor items (matched by name); the merchants with random stock
    /// (npc_vendor_random) sell the gear of their kind for every second level, and each piece gets a random quality at
    /// every restock with their chances. Reginald, the transmuter, makes the source game's crafting recipes. Also the town supplies the source game sells that had no item here yet:
    /// the Scroll of Returning, food and treasury gold.
    /// </summary>
    public static partial class TestSceneBuilder
    {
        /// <summary>Gear merchants restock this often (the source game gives no time for them).</summary>
        private const float MerchantRestockSeconds = 300f;
        /// <summary>Shops restock this often when the source game gives no time.</summary>
        private const float ShopRestockSeconds = 60f;

        /// <summary>
        /// The source game's town supplies that were missing (item_template and their spells): the Scroll of Returning
        /// (2 s cast, 5 min cooldown, back to St. Revere), food (health and mana at once, out of combat, 30 s cooldown;
        /// half the source's amounts, like our health) and treasury gold (sold back for the source price). At the end
        /// of the catalog.
        /// </summary>
        private static List<ItemDefinition> CreateTownSupplies()
        {
            var created = new List<ItemDefinition>();
            // Prices: the source game's buy price (its sell price times the buy cost ratio).
            ItemDefinition scroll = CreateItem("Supplies/ScrollOfReturning", "Scroll of Returning", ItemSlot.None, ItemRarity.Common,
                0, 0, 0, new Color(0.85f, 0.8f, 0.6f), 300);
            scroll.use = ItemUse.Return;
            scroll.castSeconds = 2f;
            scroll.useCooldown = 300f;
            scroll.maxStack = 5;
            scroll.canHaveAffix = false;
            scroll.description = "Returns you to St. Revere. The scroll is consumed when used.";
            created.Add(scroll);

            foreach ((string name, int amount, int sell) in new[]
                     {
                         ("Military Ration", 25, 5), ("Fresh Apple Pie", 75, 17), ("Gourmet Ham Sandwich", 130, 37),
                         ("Oven Baked Turkey", 250, 67), ("Roasted Pork", 400, 105),
                     })
            {
                ItemDefinition food = CreateItem($"Supplies/{name.Replace(" ", "")}", name, ItemSlot.None, ItemRarity.Common, 0, 0, 0,
                    new Color(0.8f, 0.55f, 0.3f), sell * 3);
                food.use = ItemUse.Food;
                food.restoreAmount = Mathf.RoundToInt(amount * 0.5f);
                food.useCooldown = 30f;
                food.maxStack = 10;
                food.canHaveAffix = false;
                created.Add(food);
            }

            foreach ((string name, int sell, string description) in new[]
                     {
                         ("Treasury Gold", 499, "One golden bar, prized by merchants and alchemists alike."),
                         ("Treasury Coins", 100, "Coins of St. Revere used by merchants and alchemists alike."),
                     })
            {
                // Vendors pay a quarter of the price: four times the source game's sell price.
                ItemDefinition treasure = CreateItem($"Supplies/{name.Replace(" ", "")}", name, ItemSlot.None, ItemRarity.Uncommon, 0, 0, 0,
                    new Color(1f, 0.82f, 0.25f), sell * 4);
                treasure.canHaveAffix = false;
                treasure.description = description;
                created.Add(treasure);
            }
            return created;
        }

        /// <summary>The source game's vendors in the region, with where each stands (same order).</summary>
        private static VendorDefinition[] CreateVendors(ItemCatalog catalog, out Vector3Int[] cells)
        {
            var byName = new Dictionary<string, ItemDefinition>();
            foreach (ItemDefinition item in catalog.items)
                if (item != null && !byName.ContainsKey(item.displayName)) byName[item.displayName] = item;

            var vendors = new List<VendorDefinition>();
            var places = new List<Vector3Int>();
            foreach (VendorData.Vendor source in VendorData.Vendors)
            {
                var vendor = LoadOrCreateAsset<VendorDefinition>($"{VendorsFolder}/{source.name}.asset");
                vendor.displayName = source.name;
                vendor.title = source.title;
                // The source game's transmuter (Reginald) makes its crafting recipes.
                vendor.recipes = source.title == "Transmutation" ? CreateRecipes(catalog) : Array.Empty<Recipe>();
                vendor.randomQuality = false;

                var stock = new List<ItemDefinition>();
                var limits = new List<int>();
                float restock = 0f;
                foreach ((string itemName, int limit, int restockSeconds, int _, int _) in source.items)
                {
                    if (!byName.TryGetValue(itemName, out ItemDefinition item))
                    {
                        Debug.Log($"[TestScene] {source.name}: no item \"{itemName}\" yet, left out.");
                        continue;
                    }
                    if (stock.Contains(item)) continue;
                    stock.Add(item);
                    limits.Add(limit);
                    restock = Mathf.Max(restock, restockSeconds);
                }

                if (source.gear.Length > 0)
                {
                    VendorData.GearKind first = source.gear[0];
                    vendor.randomQuality = true;
                    vendor.greenChance = first.green;
                    vendor.blueChance = first.blue;
                    vendor.goldChance = first.gold;
                    vendor.purpleChance = first.purple;
                    foreach (ItemDefinition piece in MerchantGear(catalog, source.gear))
                        if (!stock.Contains(piece))
                        {
                            stock.Add(piece);
                            // One of each: it comes back with a new quality at the next restock.
                            limits.Add(1);
                        }
                    restock = MerchantRestockSeconds;
                }

                vendor.stock = stock.ToArray();
                vendor.stockLimits = limits.ToArray();
                vendor.restockSeconds = restock > 0f ? Mathf.Max(10f, restock) : ShopRestockSeconds;
                EditorUtility.SetDirty(vendor);

                if (vendor.stock.Length == 0 && !vendor.Transmutes)
                {
                    Debug.Log($"[TestScene] {source.name} sells nothing that exists here; not placed.");
                    continue;
                }
                EditorUtility.SetDirty(vendor);
                vendors.Add(vendor);
                places.Add(source.cell);
            }
            cells = places.ToArray();
            return vendors.ToArray();
        }

        /// <summary>
        /// The gear a random-stock merchant sells: in its kind's level range, or for every second level when the source game
        /// gives none. Swords for blades, axes and maces (the warrior's weapon here), bows for ranged, staves for staves and
        /// wands; head, chest and feet armor of the light (leather, robes) or heavy kinds (chain, plate). Of each kind and
        /// level the piece with the best material chance. Necklaces, rings, belts, legs, hands and shields do not exist here
        /// and are left out.
        /// </summary>
        private static List<ItemDefinition> MerchantGear(ItemCatalog catalog, VendorData.GearKind[] kinds)
        {
            var gear = new List<ItemDefinition>();
            void Add(ItemDefinition piece)
            {
                if (piece != null && !gear.Contains(piece)) gear.Add(piece);
            }

            foreach (VendorData.GearKind kind in kinds)
            {
                IEnumerable<int> levels = kind.minLevel > 0 && kind.maxLevel >= kind.minLevel
                    ? Enumerable.Range(kind.minLevel, Mathf.Min(25, kind.maxLevel) - kind.minLevel + 1)
                    : Enumerable.Range(0, 13).Select(step => 1 + step * 2);
                foreach (int level in levels)
                {
                    if (kind.equip == 9 || kind.equip == 11)
                    {
                        int classIndex = kind.equip == 11 || kind.weapon == 2 ? RangerClass
                            : kind.weapon == 5 || kind.weapon == 7 ? MageClass : WarriorClass;
                        Add(catalog.items
                            .Where(item => item != null && item.dropWeight > 0f && item.slot == ItemSlot.Weapon && item.requiredClass == classIndex
                                           && item.itemLevel == level)
                            .OrderByDescending(item => item.dropWeight).FirstOrDefault());
                        continue;
                    }
                    ItemSlot slot = kind.equip == 1 ? ItemSlot.Head : kind.equip == 3 ? ItemSlot.Chest : kind.equip == 6 ? ItemSlot.Feet : ItemSlot.None;
                    if (slot == ItemSlot.None) continue;
                    GearData.ArmorKind[] armorKinds = kind.armor == 2
                        ? new[] { GearData.ArmorKind.Chain, GearData.ArmorKind.Plate }
                        : new[] { GearData.ArmorKind.Leather, GearData.ArmorKind.Robe };
                    foreach (GearData.ArmorKind[] group in kind.armor == 2 ? new[] { armorKinds } : armorKinds.Select(k => new[] { k }))
                        Add(GearArmorKinds
                            .Where(pair => Array.IndexOf(group, pair.Value) >= 0 && pair.Key.slot == slot && pair.Key.itemLevel == level)
                            .Select(pair => pair.Key)
                            .OrderByDescending(item => item.dropWeight).FirstOrDefault());
                }
            }
            return gear.OrderBy(item => item.itemLevel).ThenBy(item => item.slot).ToList();
        }

        private static void PlaceVendors(Grid grid, VendorDefinition[] vendors, Vector3Int[] cells, AffixCatalog affixes)
        {
            var parent = new GameObject("Vendors").transform;
            for (int i = 0; i < vendors.Length; i++)
            {
                var npc = new GameObject($"Vendor {vendors[i].displayName}");
                npc.transform.SetParent(parent, false);
                npc.transform.position = grid.GetCellCenterWorld(cells[i]);

                // A cloak colour of its own, the same every build.
                var random = new System.Random(vendors[i].displayName.Aggregate(17, (hash, c) => hash * 31 + c));
                var cloak = Color.HSVToRGB((float)random.NextDouble(), 0.45f + 0.3f * (float)random.NextDouble(), 0.45f + 0.3f * (float)random.NextDouble());
                var spriteRenderer = npc.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = PlaceholderArtGenerator.CharacterFrame(VendorArtFolder, $"Vendor_{vendors[i].displayName}", cloak,
                    Vector2.down, 0f, 0f);
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

                // Clicked to trade; does not block walking.
                var box = npc.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(0.4f, 0.7f);
                box.offset = new Vector2(0f, 0.35f);
                npc.AddComponent<Vendor>().Configure(vendors[i], affixes);
            }
        }
    }
}
