# Town, vendors and gold (after stage 10)

Inspired by the source game (an isometric tab-target MMORPG): the town has NPCs, every class has its own equipment, and the economy runs on gold.

(Since then the world, vendors and gear follow the source game's data – see [GameData.md](GameData.md).)

## What we did

- **The map is 4 times bigger:** 80×80 cells instead of 40×40. Roads run through the middle. There are 9 monster camps:

  | Where from the town | What |
  |---------------------|------|
  | Around | 5 goblin camps |
  | East | 2 Goblin Brute camps |
  | West and south-west | 2 Orc camps |

- **A town at the start:**
  - a paved square without rocks and trees around it;
  - the dungeon portal and training dummies;
  - three vendors, with their name and what they sell shown above them.
- **Vendors** (click the NPC – the character walks up and the vendor window opens on the left, the inventory on the right):

  | NPC | Where | Sells |
  |-----|-------|-------|
  | **Thorin Ironhand** | North-east | **Warrior:** Militia Sword, Steel Longsword, War Axe, Steel Helm, Steel Breastplate, Steel Sabatons |
  | **Sylva Swiftarrow** | West | **Ranger:** Hunting Bow, Longbow, Elven Bow, Leather Hood, Studded Jerkin, Ranger Boots |
  | **Elara the Arcanist** | East | **Mage:** Apprentice Staff, Runed Staff, Staff of Embers, Silk Hood, Apprentice Robe, Enchanted Robe, Silk Slippers |

  All three also sell the **Healing Potion** (10 gold).
  - Prices: Common 15–35, Uncommon 80–160, Rare 400 gold.
  - Another class cannot equip a class's items. The window shows it in red, and trying to equip one says "Your class cannot use that".
  - While the vendor window is open, **clicking an item in the bag sells it for ¼ of its price**. The description shows "Sells for N gold".
  - The window closes with X, Esc or by walking away from the vendor.
- **Gold:**
  - you start with 20;
  - monsters give it to **everyone** who fought: goblin 3–6, Goblin Brute 10–16, Orc 20–30, Goblin King 150–200;
  - a yellow "+N gold" is shown, and the inventory shows "Gold N";
  - gold is saved with the character.
- **The server checks everything:** that you stand at the vendor, have enough gold, have room in the bag, and that the item suits your class.

## Files

| File | Purpose |
|------|---------|
| `Scripts/Runtime/Vendors/VendorDefinition.cs`, `Vendor.cs` | Vendor stock and the NPC in the world |
| `Scripts/Runtime/UI/VendorWindow.cs`, `NpcNameplates.cs` | Vendor window, names above NPCs |
| `Data/Vendors/*.asset` | Vendors (created by the scene tool) |
| `Tests/PlayMode/VendorTests.cs` | New tests |

Changed files:
- `ItemDefinition`: `price` and `requiredClass` (which class it is for).
- `Inventory`: gold, buying, selling, the class check when equipping.
- `MonsterAI`: `goldReward`.
- `CharacterRecord`, `CharacterPersistence`: gold is saved.
- `LootPicker`: a clicked vendor – walk up and open the window.
- `InventoryWindow`: gold, selling, the class in the description.
- Scene tool: the map, the town, the camps, 19 new items, vendors, monster gold.

## How to check in the browser

1. Log in. Around the start there must be a paved square and three NPCs with names above their heads.
2. Click your class's vendor (e.g. Warrior – Thorin Ironhand). Its stock and your inventory open. Buy a Healing Potion or a Common item with your starting gold.
3. Click another class's vendor: its items are marked in red "... only". If you buy one, you cannot equip it.
4. While the vendor window is open, click an item in the bag – it is sold and your gold goes up.
5. Fight monsters: "+N gold" shows above the character. Once you have saved some, buy an Uncommon or Rare item.
6. Walk around the bigger map and find the Brute (east) and Orc (west) camps.

## Automatic tests

6 new `VendorTests`:
- buying costs the price and puts the item in the bag;
- without enough gold buying fails;
- selling gives ¼ of the price;
- trading far from a vendor is not allowed;
- a class item suits only its own class;
- a killed monster gives gold.

`AccountTests` now also checks that gold is saved. **94** tests in all.

## Known limitations
- Vendor stock is unlimited and prices are fixed.
- The vendor NPC is a single-frame picture without animation.
- Class items have no attack animations of their own: the ranger still swings a sword, and the projectile flies as before.
- Older saved characters that were far from the start may appear elsewhere now that the map changed. If that spot is blocked, the character appears at the start.
