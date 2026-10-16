# Stage 5 – monsters, their AI and pathfinding

## What we did

- **Goblins.** Three camps of 3 goblins, 5–9 units from the start. A goblin is smaller than the player, with a green face and a brown cloak. It has 45 HP, hits for 3–5 every 2 s, and moves in 8 directions with walk and attack animations.
- **The server decides everything.** Monster AI, movement, attacks and damage run only on the server. Clients see:
  - the position, direction and whether the monster walks – 0.15 s "in the past", smoothly, like other players;
  - HP and attack animations – through the same `Health` and `AutoAttack` as the player's.
- **Monster behaviour (`MonsterAI`):**

  | State | What happens |
  |-------|--------------|
  | **Idle** | Walks slowly around its spot (up to 1.2 units), pauses 3–8 s. |
  | **Fight** | A player comes within 3 units or hits the goblin. The goblin walks up and attacks. Idle goblins nearby (within 2.5 units) join in. |
  | **Return** | The goblin walks home and gets all its HP back. On the way home it ignores hits and takes no damage. If it gets stuck on the way for more than 10 s, it is moved home. |
  | **Death** | The grey body lies for 3 s and disappears. After 20 s the camp sends a new goblin. |

  A goblin goes home when:
  - it has **not hit** its target **for 12 s** (the player runs and the goblin does not catch up);
  - it runs more than 25 units from where the fight started. Every hit the goblin lands moves that point to where it hit, so while it keeps catching and hitting you it keeps following (changed after your note; it used to be 9 units from home);
  - everyone it fought died or ran away;
  - it cannot find a way to its target for 3 s.

- **Threat table.** A goblin attacks whoever did it the most damage. A player who only came into sight has little threat. If another player hits harder, the goblin switches to them.
- **Pathfinding on the tilemap** (`WorldNavigation` + `NavGrid`). A* searches a path over isometric cells in 8 directions:
  - goes around rocks, trees and dummies;
  - does not cut corners between obstacles;
  - the path is smoothed: where nothing is in the way it goes straight, not in steps through the cell centers.

  A search on the 40×40 map takes about 0.5 ms.
- **The player's click-to-move also goes around obstacles now.** Clicking behind a rock wall, the character walks around it instead of pushing into it. Clicking on a rock walks to the nearest free spot. Walking up to a target uses the same path.
- **Monsters spread around their target.** Several goblins do not stand on one spot.
- **Player HP regenerates** (`HealthRegeneration`). After 5 s without damage, 5 % of HP per second.

> **For now monsters do not attack from a distance** (`aggroRadius` = 0, set in the scene tool). They only fight when attacked, and their camp mates join in.

## Files

| File | Purpose |
|------|---------|
| `Scripts/Runtime/Navigation/NavGrid.cs` | A* on a cell grid, nearest free cell search (plain C#, tested without a scene) |
| `Scripts/Runtime/Navigation/WorldNavigation.cs` | Builds a `NavGrid` from the tilemaps and obstacles, finds and smooths paths in world coordinates |
| `Scripts/Runtime/Monsters/MonsterMotion.cs` | The server leads the monster along a path, clients interpolate (like other players) |
| `Scripts/Runtime/Monsters/MonsterAI.cs` | States, threat table, calling for help, going home, death |
| `Scripts/Runtime/Monsters/MonsterSpawner.cs` | Camp: sends monsters when the server starts and after deaths |
| `Scripts/Runtime/Combat/HealthRegeneration.cs` | HP regeneration out of combat |
| `Prefabs/Monsters/Goblin.prefab` | Goblin (created by the scene tool) |
| `Data/Characters/Goblin_AnimationSet.asset`, `Art/Placeholder/Goblin/*.png` | Goblin animations and temporary frames (created by the scene tool) |
| `Tests/PlayMode/NavigationTests.cs`, `MonsterTests.cs` | New tests |

Changed files:
- `Health`:
  - `ApplyDamage` now takes who hit;
  - added the `DamageTaken` event, `ServerInvulnerable` and `Heal()`.
- `AutoAttack`: passes the attacker to `ApplyDamage`.
- `PlayerMovement`: click-to-move follows the path `WorldNavigation` finds. Without navigation in the scene it walks straight, as before.
- Scene tool:
  - creates the goblin prefab and registers it in `DefaultNetworkPrefabs`;
  - adds `WorldNavigation` to the `Grid`;
  - creates 3 camps (`Monster Camps`);
  - adds `HealthRegeneration` to the player.
- `PlaceholderArtGenerator.CharacterFrame`: the face colour can be given.

## How to check in the browser

https://cocomelionas-netizen.github.io/RPG-s/ (if it shows an old version, reload with Ctrl+F5)

1. **Find the goblins.** From the start go up and a little right (~5 units), left, or right and down (~7 units). Goblins wander slowly in their camps.
2. **Come within ~3 units.** The goblin must notice you, come and start hitting: you see red damage numbers and your HP at the bottom going down. Its friends should come running too.
3. **Fight:** select a goblin (mouse or **Tab**) and press **1** (Attack). A killed goblin turns grey and disappears after 3 s. After ~20 s a new one appears in the camp.
4. **Run far away** (more than ~25 units from where the goblin last hit you). The goblin turns around, goes home, its HP fills up, and you cannot hurt it on the way.
5. **Let the goblins beat you.** You respawn at the start and the goblins go back to their camp.
6. **Rest** – after 5 s without damage your HP slowly comes back.
7. **Pathfinding:**
   - click behind the little rock wall (right of and above the start) or behind a tree – the character must walk around it, not push into it;
   - chase a goblin behind an obstacle – it walks around too.

## Automatic tests

In the Unity editor: first `RPG > Build Test Scene` (the tests use the goblin prefab), then `Window > General > Test Runner` → **PlayMode** → **Run All**. There must be **57** tests, 14 of them new:
- `NavigationTests` (6):
  - a path through a gap in a wall;
  - a blocked target;
  - corners are not cut;
  - nearest free cell;
  - a path around rocks on a real tilemap;
  - a click on a rock.
- `MonsterTests` (8):
  - a goblin attacks a nearby player and not a far one;
  - chases the player;
  - turns to whoever hit it;
  - goes home invulnerable and heals;
  - disappears when dead;
  - the camp sends a new one;
  - the player's HP regenerates.

These tests were written in the cloud without Unity. The code was checked with the .NET compiler and the `NavGrid` logic with separate tests. I could not run the PlayMode tests, so run them yourself the first time.

## Where to change the numbers
- Goblin HP, damage, attack speed: `Goblin.prefab` → `Health`, `AutoAttack`.
- Sight range, help radius, leash distance, wandering, corpse time: `Goblin.prefab` → `MonsterAI`.
- Speed: `Goblin.prefab` → `MonsterMotion`.
- Camp location, how many monsters and how fast they come back: `TestSceneBuilder.PlaceMonsterCamps`.

The prefabs and the scene are created by `RPG > Build Test Scene`, and CI does it before every build. So change numbers in the scene tool, not in the prefab by hand.

## Known limitations
- Monsters pass through each other and through players, like players among themselves. They go around obstacles by pathfinding, not physics.
- There is only one monster type for now. More types, loot and experience come in stages 6–7.
- Monsters are not limited to nearby players (interest management). That will be needed when the world grows.
- Map navigation is built once. If the map later changes during play (e.g. doors), `WorldNavigation.Build()` must be called.
