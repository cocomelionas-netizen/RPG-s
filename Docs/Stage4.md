# Stage 4 – targeting and real-time combat

## What we did

- **Health (`Health`).** The player has 100 HP, the training dummy 200. Only the server holds and changes HP; clients only read it.
- **Factions (`Faction`).** Players can only attack units of another faction (monsters). PvP is off for now (stage 11): another player can be selected but not attacked.
- **Targeting** (`PlayerTargeting`):

  | Action | What happens |
  |--------|--------------|
  | Left click on an enemy | Selects it (since the stage 6 fix it does not attack) |
  | Left click on the ground | Stops attacking and walks there (the target stays selected) |
  | WASD | Stops attacking (the target stays) |
  | **Tab** | Next nearest enemy (up to 10 units) |
  | **1** or **F** ("Attack" button) | Attack the selected target: the character walks into range and attacks by itself |
  | **Esc** | Clear the target |

- **Auto-attack (`AutoAttack`).** The server decides all of it. The client only asks "attack this one", and the server checks:
  - that the target is an enemy and alive;
  - that the distance is at most 0.9 units (with 0.35 tolerance for latency);
  - that 1.5 s have passed since the last hit.

  The server computes the damage (8–12, 10 % chance of a ×2 critical hit) and everyone sees the attack animation. Faked messages cannot cheat: the server checks the position it has, not the one the client reports.
- **Death and respawn** (`PlayerRespawn`). At 0 HP:
  - the character turns grey and half transparent and cannot move;
  - after 5 s the server moves it to the start with full HP.
- **Training dummies** (2 next to the start):
  - do not hit back;
  - left alone for 6 s they return to full HP;
  - "dead", they stand up again after 4 s.
- **Visuals:**
  - **top** – the target frame (name, HP bar, numbers);
  - **bottom** – your frame;
  - **above the head** – an HP bar while the unit is hurt or selected;
  - **red ring** under the selected target;
  - **damage numbers** rise and fade: white – your damage, yellow with "!" – a critical hit, red – damage to you;
  - **red flash** on a hit;
  - **attack animation** – 3 frames with a sword, 8 directions (the west side mirrored).

## Files

| File | Purpose |
|------|---------|
| `Scripts/Runtime/Combat/Health.cs` | HP, faction, name; damage and respawn on the server |
| `Scripts/Runtime/Combat/Faction.cs` | Factions |
| `Scripts/Runtime/Combat/AutoAttack.cs` | Server attack: range, interval, damage, animation |
| `Scripts/Runtime/Combat/PlayerTargeting.cs` | Mouse, Tab, F, Esc; walking up to the target |
| `Scripts/Runtime/Combat/PlayerRespawn.cs` | Player respawn |
| `Scripts/Runtime/Combat/TrainingDummy.cs` | Dummy behaviour |
| `Scripts/Runtime/Combat/HealthPresenter.cs` | Bar above the head, flash, damage numbers, death look |
| `Scripts/Runtime/Combat/TargetIndicator.cs` | Ring under the target |
| `Scripts/Runtime/UI/UnitFrame.cs` | Your frame and the target frame |
| `Scripts/Runtime/UI/CombatTextLayer.cs` | Floating damage numbers |
| `Prefabs/World/TrainingDummy.prefab` | Dummy |
| `Tests/PlayMode/CombatTests.cs` | Combat tests on a real Host |

Changed files:
- `PlayerInputReader`: added Tab, F and Esc.
- `PlayerMovement`: added `FaceTowards()` and `SuppressClickMoveUntilRelease()` (so a click on an enemy is not also a move command).
- `PlayerNetworkMovement`: a dead player cannot move; added `ServerTeleport()` for respawning.
- `NetworkBootstrap`: added a public `GetSpawnPosition()`.
- Scene tool: creates the dummies, the HUD and the target ring; the Player prefab now has the combat components and a "Hitbox" (a trigger for mouse picking that does not block movement).

## How to check

1. `RPG > Build Test Scene`, **Play**, **Host**.
2. **Click the dummy** (right of the start):
   - a red ring appears under it and its frame at the top;
   - the character walks up and starts hitting: you see the sword swing, damage numbers and the HP bar shrinking.
3. **Click the ground** – the attack stops, the frame stays. **F** – the attack starts again.
4. **Tab** switches between the two dummies, **Esc** clears the target.
5. **Take a dummy down to 0** – it turns grey and stands up after 4 s. Left hurt, it heals after 6 s.
6. **Multiplayer:** `RPG > Run > Local Server` and `Local Bot Client`, **Client** in the editor. Combat works the same and the server computes the damage. Checked: dummy 200 → 146 → 35 HP through a real server process.

### Automatic tests
`Window > General > Test Runner` → **PlayMode** → **Run All** – **43** tests, 7 of them new combat tests:
- full HP and name;
- one hit only every 1.5 s;
- too far – no damage;
- a friend cannot be attacked;
- the attack stops when the target dies;
- the dummy stands up;
- the player dies, cannot move and respawns at the start.

## Where to change the numbers
- Player damage, attack speed, range, critical chance: `Player.prefab` → the `AutoAttack` component. In stages 6–7 this moves to classes and weapons.
- HP: the `Health` component (`Max Health`).
- Respawn time: `PlayerRespawn` and `TrainingDummy`.

If you change prefabs by hand, remember: `RPG > Build Test Scene` creates them again.

## Known limitations
- For now only tests can hurt the player, because the dummies do not hit back. Real monsters with AI come in stage 5.
- Melee attacks only. Abilities, ranged attacks and classes – stage 6.
- The UI uses the plain Unity font and coloured rectangles. A nice painted UI comes later, together with real art.
