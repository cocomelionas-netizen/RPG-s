# Chests, elite monsters and ranks

## Chests

| Chest | Level | Gold | Inside | Comes back |
|---|---|---|---|---|
| Wooden Crate | 2 | 3–8 | potions, junk, plain items | after 2 min |
| Oak Chest | 4 | 10–25 | Uncommon items, Chipped gems, Lair Key | after 4 min |
| Iron Strongbox | 6 | 30–60 | Rare/Epic items, gems, Lair Key | after 7 min |

- 8 crates by the goblin camps and around the town, 2 chests in Brute Hills, 2 strongboxes in Orc Marsh.
- Clicking walks up and opens it: the gold goes straight to the purse, items into a loot bag (with affixes and sockets).
- An opened chest disappears, and its `ChestSpawner` puts a new one there after a while. Above the chest – its name and level; on the minimap – an orange dot.

(Since then chests stand where the source game has its containers, with its contents and locks – see [GameData.md](GameData.md#chests).)

## Elite monsters

- ~8% of camp monsters appear as elites (`MonsterSpawner.eliteChance`): named "Elite …",
  25% bigger, 2.5× HP, 1.5× damage, 3× XP and gold, loot rolled twice.

## Ranks

- The top level is 25. Every level has a rank: Recruit, Private, Lance Corporal, … General,
  Field Marshal, Knight, Baron, Count, Earl, Champion (`LevelCurve.RankName`).
- Above every player's head (yours too) – the name, and under it the rank, e.g. "<Corporal>". Colours as in the source game: players light blue (#8EACD2), monsters red (#B63E3D), chests nearly white (#E8EDE1).
- The rank is also shown in the frame, the character window (C) and the level-up message.

Tests: `ChestTests.cs` (chests and elites), `LevelCurveTests.cs` (ranks).
