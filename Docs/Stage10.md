# Stage 10 – dungeons (instances)

> The single hand-made "Goblin Den" of this stage has since been replaced by the ten dungeons laid out from the
> source game's data (see [GameData.md](GameData.md#dungeons)). The instance system below (copies per group, portals,
> closing empty copies, the boss slam) is what they run on.

## What we did

- **The "Goblin Den" dungeon.**
  - The entrance is a purple portal a little south-west of the start.
  - Click the portal: the character walks up, and **all living players within 3 units of the portal** (your group) are moved together into a new copy of the dungeon.
  - Other groups that enter later get **their own copy**, so monsters and loot are not shared between groups.
- **Dungeon plan:** 4 rooms in a row joined by corridors. The walls are real obstacles and pathfinding works inside.

  | Room | What is there |
  |------|---------------|
  | 1 | Arrival and the exit portal (always open) |
  | 2 | 2 Goblin Brutes |
  | 3 | 2 Orcs and 1 Goblin Brute |
  | 4 (big) | **Goblin King** and a second exit that only opens once the boss is beaten |

  **Dungeon monsters attack by themselves** when you come within ~3.5 units. Outdoor monsters still do not.
- **The Goblin King boss:** level 8, 900 HP, 14–20 damage, 300 XP. Every 9 s (the first time 4 s into the fight) it does a **Slam**:
  - a **red ring** pulses on the ground around it for 1.5 s;
  - then every player still inside the ring takes 25–35 damage;
  - a player who got out of the ring in time takes nothing.
- **Boss loot:** always 2 Healing Potions, Orcish Greataxe and Orcish Plate 50 % each, Orc Skull Helm and Goblin Cleaver 40 % each.
- **Leaving:** through the portal in the first room or, once the boss is beaten, through the portal in the boss room. You return to the entrance in the world. Dying in the dungeon, you respawn at the start.
- **Closing a dungeon:** when **no player has been inside for 20 s**, the server closes it with its monsters and untaken loot. Entering again, the dungeon is full again.
- **How it works inside:**
  - a dungeon is a separate part of the map in the same scene, far from the world (from x = 500, every 150 units a new copy);
  - the server sends only the copy's number over the network, and every client builds the walls, floor, navigation and portals itself from the same plan (`DungeonLayout`);
  - the server runs the monsters, the boss and the dungeon itself;
  - navigation supports several maps at once (`WorldNavigation.At`);
  - on a long jump (portal, respawn) the camera jumps with the character.
- **Saving:** if you log out in a dungeon, next time you appear at the start, because the copy will have been closed.

## Files

| File | Purpose |
|------|---------|
| `Scripts/Runtime/Dungeons/DungeonLayout.cs` | The dungeon plan in cells: rooms, corridors, walls, arrival and exit spots |
| `Scripts/Runtime/Dungeons/DungeonDefinition.cs` | Dungeon description: tiles, portal look, monster packs, boss |
| `Scripts/Runtime/Dungeons/DungeonInstance.cs` | One copy (network object): number, whether the boss is beaten, its monsters |
| `Scripts/Runtime/Dungeons/DungeonManager.cs` | Creating, building, entering, leaving and closing copies |
| `Scripts/Runtime/Dungeons/Portal.cs`, `DungeonTraveller.cs` | Portals and the player's request to use them |
| `Scripts/Runtime/Dungeons/BossMechanics.cs` | The boss Slam with its warning |
| `Data/Dungeons/GoblinDen.asset`, `Prefabs/World/DungeonInstance.prefab`, `Prefabs/Monsters/GoblinKing.prefab` | Created by the scene tool |
| `Tests/PlayMode/DungeonTests.cs` | New tests |

Changed files:
- `WorldNavigation`: several maps, `At(position)` instead of `Instance`.
- `LootPicker`: a clicked portal – walk up and use it.
- `PlayerNetworkMovement`: the camera jumps on a long teleport.
- `CombatEffects`: `Telegraph` (a pulsing warning ring).
- `CharacterPersistence`: restores the position only when it is on the map.
- Scene tool: the dungeon, the boss, the entrance portal, `DungeonManager`, `DungeonTraveller` for the player.

## How to check in the browser

https://cocomelionas-netizen.github.io/RPG-s/ (if it shows an old version, reload with Ctrl+F5)

1. Log in. Next to the start, a little lower and to the left, stands a **purple portal**. Click it: the character walks up, "Entering Goblin Den" shows, and you are in a dark dungeon.
2. Go right through the corridors. Monsters attack by themselves when you come close. Being at least level ~4–5 with potions is advised.
3. The **Goblin King** waits in the last room. Watch the **red ring** under it and get out of it in time.
4. Once the boss is beaten, loot drops and the exit portal at the end of the boss room opens (no longer faded). Click it: you return to the entrance in the world.
5. Leave, wait ~20 s and enter again: the dungeon is full of monsters again.

## Automatic tests

**PlayMode** → **Run All**. 5 new `DungeonTests`:
- entering creates a copy with navigation, monsters and the boss, and the player is inside;
- leaving returns to the entrance;
- the boss exit is closed while the boss lives;
- an empty copy is closed with its monsters;
- the Slam hits a player in the ring only after the warning.

**88** tests in all. The code was checked with the .NET compiler; PlayMode tests were not run in the cloud.

## Where to change things
- Rooms and corridors: `DungeonLayout`.
- Monster packs, the boss, tiles: `TestSceneBuilder.CreateDungeon`.
- Boss strength and loot: `TestSceneBuilder` ("Goblin King").
- Slam: `GoblinKing.prefab` → `BossMechanics`.
- Group radius, closing time: `Dungeon Manager` in the scene.

## Known limitations
- There is no real party system. A group is everyone standing at the portal.
- One dungeon, one plan. There are no randomly generated dungeons.
- All copies are in the same scene, so clients also get information about other groups' monsters (no interest management yet). It will be needed when there are many players.
- Outdoor monsters still do not attack from a distance, as you asked.

## Entry requirements

- `DungeonDefinition.minLevel` (Goblin Den: 3), `key` (Den Key) and `maxPlayers` (4).
- The player who clicks the portal must be of the required level and carry the key — one key is used up.
  Other players at the portal go along if they meet the level (they need no key), up to `maxPlayers`.
- The reason for a refusal is shown above the head and in the message log ("You need a Den Key to enter Goblin Den").
- Den Keys drop from monsters: Goblin 4%, Goblin Brute 25%, Orc 35%.

## Limited vendor stock

- `VendorDefinition.stockLimits` (0 = unlimited) and `restockSeconds` (300 s).
  Class vendors: Rare items — 1, Uncommon — 2, Common and potions — unlimited.
- The server keeps what is left (`Vendor.ServerRemaining`); the client gets it when the window opens and after a purchase;
  the window shows "(2 left)" or "(sold out)".
