# Stage 6 – classes, abilities, levels and experience

## What we did

- **Three classes:**

  | Class | HP (lvl 1 / +lvl) | Attack | Abilities (1 / 2 / 3) |
  |-------|-------------------|--------|-----------------------|
  | **Warrior** (blue cloak) | 110 / +11 | melee, 8–12 damage every 1.5 s | **Heavy Strike** – ×2.2 damage to the target (6 s); **Whirlwind** – ×1.3 to everyone around (radius 1.4, 10 s); **Second Wind** – +30 % HP (30 s) |
  | **Ranger** (green) | 90 / +9 | from 5 units, 6–9 every 1.7 s, 15 % critical chance | **Aimed Shot** – ×2.2 from 6 units (7 s); **Volley** – ×1.2 to everyone around the target (10 s); **Mend** – +25 % HP (30 s) |
  | **Mage** (purple) | 80 / +8 | from 4.5 units, 5–8 every 1.8 s | **Fireball** – ×2.8 from 5.5 units (5 s); **Flamestrike** – ×1.8 to everyone around the target (12 s); **Frost Nova** – ×1.4 to everyone around yourself (14 s) |

  Damage grows by +1 (Ranger and Mage +0.8) per level. Ranged classes shoot a coloured projectile.
- **Choosing a class.** Top right there are **Warrior / Ranger / Mage** buttons. For now the class can be changed at any time, to make testing easy. Level and experience stay. When accounts come in stage 8, the class is chosen when creating a character.
- **Action bar** (fixed after your note):
  - clicking a monster only **selects** it – the character does not attack;
  - **1 – Attack:** the character runs to the selected enemy and attacks. Without a target it shows "No enemy target". **F** does the same;
  - **2, 3, 4 – abilities.** If the target is too far, the character runs into the ability's range by itself and then uses it. The button is highlighted in gold meanwhile. WASD, a click on the ground, Esc or choosing another target cancels the run.
- **Ability buttons** show:
  - the name;
  - a darkening cooldown overlay with seconds;
  - the messages "Out of range", "No enemy target", "... is not ready".
- **The server checks everything.** It checks that:
  - you are alive;
  - the ability is ready (its own cooldown and the shared 1 s);
  - the target is a living enemy and close enough.

  Then the server computes damage and healing. The client only asks, so cooldowns and range cannot be cheated. Area abilities hit every enemy in the radius.
- **Experience and levels** (up to 20):
  - a goblin (level 2) gives 20 XP to **everyone** who fought it;
  - if you are above the monster's level you get 20 % less XP per level of difference (at least 10 %);
  - level 2 needs 60 XP, 3 – 133, 5 – 340, 10 – 1100.

  Levelling up raises HP and damage and fills HP. A golden ring and "Level N!" are shown.
- **HUD:**
  - bottom – the experience bar ("Level 2 Warrior – 45 / 133 XP");
  - above your frame – the ability buttons;
  - frames show level and class, e.g. "Player 1 Lv 3 Ranger" or "Goblin Lv 2";
  - floating text: "+20 XP" (purple), "+33" healing (green), "Level 3!" (gold).
- **Effects.** Projectiles, expanding rings on the ground (Whirlwind, Frost Nova, Volley, Flamestrike) and a healing ring. They show on every client, but only as visuals: the server decides hits.

## Files

| File | Purpose |
|------|---------|
| `Scripts/Runtime/Progression/CharacterClassDefinition.cs` | Class: look, stats at level 1, growth, abilities |
| `Scripts/Runtime/Progression/AbilityDefinition.cs` | Ability: kind, cooldown, range, radius, damage multiplier, healing, colour |
| `Scripts/Runtime/Progression/ClassCatalog.cs` | List of all classes (the player stores the class number) |
| `Scripts/Runtime/Progression/LevelCurve.cs` | XP needed per level, XP per monster |
| `Scripts/Runtime/Progression/CharacterProgress.cs` | Player class, level, XP (server), applying stats on all clients |
| `Scripts/Runtime/Progression/AbilityCaster.cs` | Using abilities: client request, server check and effect, cooldowns |
| `Scripts/Runtime/Combat/CombatEffects.cs` | Projectiles and rings |
| `Scripts/Runtime/UI/AbilityBar.cs`, `ExperienceBar.cs`, `ClassPicker.cs`, `UIFactory.cs` | New HUD elements (the UI builds itself) |
| `Data/Classes/*.asset`, `Data/Abilities/*.asset` | Classes and abilities (created by the scene tool) |
| `Tests/PlayMode/LevelCurveTests.cs`, `ProgressionTests.cs` | New tests |

Changed files:
- `Health`:
  - a public list of all units, `Spawned`;
  - `ServerSetMaxHealth`;
  - `Heal(amount, announce)` with the `Healed` event.
- `AutoAttack`:
  - `SetStats` and `RollDamage`;
  - the ranged attack projectile.
- `MonsterAI`: monster level and XP reward for everyone who fought.
- `PlayerInputReader`: keys 1–5.
- `UnitFrame`: level and class.
- `HealthPresenter`: green healing numbers.
- `CombatTextLayer`: `ShowText`.
- Scene tool:
  - Ranger and Mage looks;
  - class and ability assets;
  - `CharacterProgress` and `AbilityCaster` for the player;
  - goblin level and XP;
  - the `Combat Effects` object and new HUD elements.

## How to check in the browser

https://cocomelionas-netizen.github.io/RPG-s/ (if it shows an old version, reload with Ctrl+F5)

1. **HUD.** At the bottom there must be the experience bar "Level 1 Warrior – 0 / 60 XP", above your frame 3 ability buttons, top right the class buttons.
2. **Warrior.** Walk up to a dummy and select it:
   - **1** (Attack) – the character runs up and attacks;
   - **2** (Heavy Strike) – a big number and a 6 s cooldown on the button;
   - **3** (Whirlwind) – a ring around you, hits everyone close;
   - **4** (Second Wind) – a green "+N" after taking damage.
3. **Target and range.** Press **1** without a target – "No enemy target". Select a far goblin and press **2** – the character runs up and strikes.
4. **Ranger.** Choose it top right: the character turns green, HP changes, and it attacks from afar with projectiles. Try **2** and **3**.
5. **Mage.** Choose it: the character turns purple. Try Fireball, Flamestrike and Frost Nova.
6. **Levels.** Kill 3 goblins: each gives "+20 XP", and after the third "Level 2!" shows. HP grows and fills up.

## Automatic tests

In the Unity editor: `RPG > Build Test Scene`, then `Window > General > Test Runner` → **PlayMode** → **Run All**. There must be **68** tests, 11 of them new:
- `LevelCurveTests`: XP curve, several levels at once, max level, less XP from lower-level monsters;
- `ProgressionTests`:
  - starting as a level 1 Warrior;
  - levelling up raises HP and heals;
  - changing class changes the stats;
  - XP per monster;
  - ability cooldown;
  - too far or no target;
  - an area ability hits only those close;
  - healing.

As in stage 5, the code was checked with the .NET compiler and the XP curve separately. I cannot run PlayMode tests in the cloud.

## Where to change the numbers
- Class stats and abilities: `TestSceneBuilder.CreateClasses`. The scene tool rewrites the assets in `Data/Classes`, `Data/Abilities` every time.
- XP curve and XP reduction: `LevelCurve`.
- Goblin level and XP: `TestSceneBuilder.CreateGoblinPrefab`.
- Shared cooldown: `AbilityCaster.GlobalCooldown`.

## Known limitations
- **Progress is not saved.** After a page reload or disconnect you are level 1 again. Saving on the server comes in stage 8.
- There is only one monster type for now (level 2 goblins), so from ~level 4 XP comes very slowly. Stronger monsters are needed together with loot (stage 7).
- No mana or energy: abilities are limited only by cooldowns.
- Abilities have no status effects (slow, stun) and are instant, without a cast time.
- The class buttons are for testing. With accounts, the class is chosen when creating a character.
