# Stage 1 – isometric map, camera, movement

## What we did

- **Isometric world.** A `Grid` with the Isometric layout and 1 × 0.5 cells. It holds two tilemaps:
  - `Ground` – grass and paths, no physics, sorting order -100;
  - `Obstacles` – rocks with a `TilemapCollider2D`; the edges are closed so the player cannot leave the map.
- **Isometric sorting.** Transparency Sort Mode = Custom Axis (0, 1, 0). Whatever is lower on screen is drawn in front, so the character can walk behind a rock.
- **Character** (`Player.prefab`). A `Rigidbody2D` without gravity, a small `CircleCollider2D` at the feet (in isometric view only the "feet" collide), `PlayerInputReader` and `PlayerMovement`.
- **Movement:**
  - WASD or arrow keys – moves in screen directions;
  - left mouse button – click-to-move; holding the button makes the character follow the cursor;
  - the keyboard always cancels a mouse target;
  - if the character is blocked by an obstacle for more than 0.25 s, the mouse target is cancelled.
- **Camera** (`CameraFollow`). Orthographic, follows the player smoothly (SmoothDamp); the mouse wheel zooms from 2.5 to 8.
- **Global Light 2D** and **EventSystem**. The EventSystem is needed so that clicks on the UI do not move the character.

## Files

| File | Purpose |
|------|---------|
| `Assets/_Project/Scripts/Runtime/Controls/PlayerInputReader.cs` | Reads the keyboard and mouse through the Input System |
| `Assets/_Project/Scripts/Runtime/Player/PlayerMovement.cs` | WASD and click-to-move movement through `Rigidbody2D` |
| `Assets/_Project/Scripts/Runtime/CameraSystem/CameraFollow.cs` | Camera follow and zoom |
| `Assets/_Project/Scripts/Runtime/RPG.Runtime.asmdef` | Runtime code assembly |
| `Assets/_Project/Scripts/Editor/TestSceneBuilder.cs` | Menu `RPG > Build Test Scene`: creates the 2D Renderer settings, tiles, the prefab and the scene (renamed from `Stage1SceneBuilder` in stage 2) |
| `Assets/_Project/Scripts/Editor/PlaceholderArtGenerator.cs` | Generates temporary sprites |
| `Assets/_Project/Tests/PlayMode/*.cs` | Automatic movement and camera tests |
| `Assets/_Project/Scenes/World_Test.unity` | Test scene |

## How to check

### By hand
1. Open `Assets/_Project/Scenes/World_Test.unity` and press **Play**.
2. **WASD / arrows** – the character moves, the camera follows smoothly.
3. **Left click** on the ground – the character walks there and stops.
4. **Hold the mouse button** and move it – the character follows the cursor.
5. **Click behind a rock** – the character bumps into it, stands briefly and the target is cancelled (it does not walk "into the wall" forever).
6. **Walk up to the little rock wall** (right of and above the start) from above and from below. From above the character must be covered by the rock, from below it must be in front of it.
7. **Mouse wheel** – zooms in and out.
8. **Console** – no red errors. The message "GPUResidentDrawer Disabled…" is normal with the 2D Renderer.

### Automatic tests
`Window > General > Test Runner` → tab **PlayMode** → **Run All**. At the end of stage 1 there were 6 tests (24 in stage 2):
- `PlayerMovementTests`: reaches the target, updates the facing, does not pass through a wall and cancels the target, `Stop()` stops;
- `CameraFollowTests`: the camera catches up with the target and keeps the Z depth, `Follow(..., snap: true)` jumps at once.

### Rebuilding the scene
If the scene is broken: `RPG > Build Test Scene`. The tool generates everything again (the map is the same every time, seed 12345).

## Replacing the art

The placeholders are already set up for painted HD art, so real files can simply take their place:
- **256 pixels per unit**, Filter Mode Bilinear, no pixel-art settings;
- **ground tile** – a 256 × 128 px diamond, pivot in the center;
- **tall objects** (rocks, trees) – pivot at the base so sorting is correct;
- **character** – pivot at the feet (`SpriteSortPoint.Pivot`).

## Known limitations (to solve later)
- ~~The character has no animations~~ – done in stage 2.
- ~~Click-to-move walks in a straight line~~ – pathfinding done in stage 5.
- Everything works only locally, without networking (stage 3).
