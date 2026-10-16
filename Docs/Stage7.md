# Stage 7 – inventory, weapons, armor, loot

## What we did

- **Items** (`ItemDefinition`, `ItemCatalog`):
  - four equipment slots: weapon, helmet, armor and boots, plus healing potions;
  - rarity is shown by colour: Common (white), Uncommon (green), Rare (blue), Epic (purple);
  - items add **damage**, **HP** and **armor**. Armor reduces damage taken: 100 armor halves it, 12 armor about 11 %.

  | Slot | Common | Uncommon | Rare | Epic |
  |------|--------|----------|------|------|
  | Weapon | Rusty Sword +2 damage | Iron Sword +4 | Goblin Cleaver +7, +10 HP | Orcish Greataxe +12, +20 HP |
  | Helmet | Leather Cap 4 armor | Iron Helm 8, +5 HP | Orc Skull Helm 12, +15 HP | |
  | Armor | Padded Vest 6 | Chainmail 12, +10 HP | | Orcish Plate 22, +30 HP |
  | Boots | Worn Boots 3 | Iron Boots 6, +5 HP | | |

  A **Healing Potion** restores 35 % HP. Potions stack up to 10 and have a 5 s cooldown between drinks. Every new character gets 3 potions.
- **Inventory** (`Inventory`): a 20-slot bag and 4 equipment slots. The server holds all of it. The client only asks to equip, unequip, drink or pick up, and the server checks whether that is allowed.
- **Inventory window**: key **I** (or **B**) or the **Bag (I)** button bottom right.
  - **Click an item in the bag** to equip it. A potion is drunk instead. If the slot already had an item, it goes back to the bag.
  - **Click an equipped item** to take it off.
  - Hovering shows the item's description.
  - The current damage, HP and armor are shown at the top.
- **Loot.** A killed monster may leave a **bag** on the ground according to its loot table. Click it: the character walks up and takes everything that fits. "+ Iron Helm" is shown in the item's rarity colour. The bag disappears when emptied or after 90 s. The server checks that the player is close enough.
- **Stronger monsters and their camps:**

  | Monster | Level | HP | Damage | XP | Camp | Best loot |
  |---------|-------|----|--------|----|------|-----------|
  | Goblin | 2 | 45 | 3–5 | 20 | 3 camps of 3, 5–7 units from the start | Iron Sword (3 %) |
  | Goblin Brute (grey, bigger) | 4 | 120 | 6–10 | 45 | 2, ~10 units to the right | Goblin Cleaver (4 %) |
  | Orc (red, biggest) | 6 | 220 | 10–15 | 90 | 2, ~10 units to the left | Orcish Plate, Orcish Greataxe (5 % each) |

  Stronger monsters come back after 40 s, goblins after 20 s.

## Files

| File | Purpose |
|------|---------|
| `Scripts/Runtime/Items/ItemDefinition.cs`, `ItemCatalog.cs`, `ItemStack.cs`, `LootEntry.cs` | Items, their catalog (an item is stored as a number), stacks, loot entries |
| `Scripts/Runtime/Items/Inventory.cs` | Bag and equipment (server), item bonuses, potions, pickup |
| `Scripts/Runtime/Items/LootBag.cs`, `LootPicker.cs` | Bag on the ground; clicking, walking up and picking up |
| `Scripts/Runtime/UI/InventoryWindow.cs`, `ItemSlotView.cs` | Inventory window |
| `Data/Items/*.asset`, `Prefabs/World/LootBag.prefab`, `Prefabs/Monsters/*.prefab` | Created by the scene tool |
| `Tests/PlayMode/InventoryTests.cs` | New tests |

Changed files:
- `Health`: `ServerArmor` and `ReduceByArmor` (damage reduction).
- `CharacterProgress`: adds item damage, HP and armor to the class and level.
- `MonsterAI`: loot table and a bag on death.
- `PlayerInputReader`: keys I and B.
- Scene tool:
  - items and the catalog;
  - shared monster prefab creation (`MonsterSpec`);
  - Goblin Brute and Orc with camps;
  - the loot bag;
  - `Inventory` with 3 potions and `LootPicker` for the player;
  - the inventory window.

## How to check in the browser

https://cocomelionas-netizen.github.io/RPG-s/ (if it shows an old version, reload with Ctrl+F5)

1. **I** – the inventory opens with 3 Healing Potions. Hover to see the description.
2. Take damage from a goblin and click a potion: a green "+N" shows, 2 potions are left. Drinking a second at once fails ("Not ready yet").
3. Fight goblins until one leaves a bag (a potion drops 30 % of the time, items more rarely). Click the bag: the character walks up and "+ item" shows.
4. Click an item in the bag: it appears in its equipment slot and Damage, Health or Armor at the top goes up. Click it there to take it off.
5. Visit the Goblin Brute (right) and Orc (left) camps. They are more dangerous but give more XP and better loot.

## Automatic tests

`RPG > Build Test Scene`, then **PlayMode** → **Run All**. 7 new `InventoryTests`:
- starting potions;
- a weapon adds damage, and taking it off removes it;
- armor adds HP and reduces damage;
- a potion heals, is used up and has a cooldown;
- a full bag takes no more items;
- a monster leaves a bag that can be picked up;
- a bag too far away cannot be picked up.

With the tests of the stage 5–6 fixes there are **78** tests in all. As before, the code was checked with the .NET compiler, but PlayMode tests were not run in the cloud.

## Where to change the numbers
- Items and their stats: `TestSceneBuilder.CreateItems`. Always add new items at the end of the list, because players store the item number.
- Monsters, their loot chances and camps: `TestSceneBuilder` (`MonsterSpec`, `PlaceMonsterCamps`).
- Bag size, potion cooldown, pickup range: `Player.prefab` → `Inventory`.
- Armor formula: `Health.ReduceByArmor`.

## Known limitations
- **Items are not saved.** They disappear after a page reload. Saving on the server comes in stage 8.
- Items are shown as coloured squares with a name, because there are no icons yet.
- Items cannot be dropped, moved between bag slots or sold. Merchants were planned with NPCs (stage 9), which was dropped.
- Equipped items do not change the character's look.
- Loot does not belong to a particular player: whoever gets there first can take it.
