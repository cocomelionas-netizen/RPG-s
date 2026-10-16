# Stage 2 – character presentation

## What we did

- **8-direction animations.** The character is shown facing the way it moves: E, NE, N, NW, W, SW, S or SE (directions are computed on screen).
  - Animations live in a `CharacterAnimationSet` asset: each animation (Idle, Walk and others) has frames for each direction.
  - **Mirroring.** When a west-side direction (NW, W, SW) has no frames, the east-side frames are used, flipped horizontally. An artist only needs to draw 5 directions instead of 8.
  - When a direction has no frames at all, the south (S) frames are shown, so the character never disappears.
  - `PlayOneShot("Attack")` plays a one-off animation that returns to Idle or Walk when done. It is used in combat (stage 4).
- **Animation is separate from control.** `CharacterAnimator` does not read `PlayerMovement`; it reads the `ICharacterMotionSource` interface. The same component works for monsters (AI) and other players (network).
- **A marker where you clicked** (`ClickMarker`). A golden ring pulses on the ground. It disappears when the character arrives, the movement is cancelled or WASD movement starts.
- **Trees** (`Tree.prefab`). Tall objects with the pivot at the base of the trunk, so the character can walk behind the crown. Only the trunk collides, not the whole crown.
- **Import rules for HD art** (`ArtImportRules`). An image dropped into `Assets/_Project/Art/` automatically gets: Sprite, 256 PPU, Bilinear, high-quality compression and the right pivot.
- **Fixed a mouse movement bug.** When a frame took longer than 0.25 s (lag or an unfocused window), the target was cancelled before the character had moved. Mouse movement is now also handled in the physics step, and being stuck is only checked when movement was attempted.
- The scene tool was renamed: it is now **`RPG > Build Test Scene`** (previously `RPG > Stage 1 > Build Test Scene`).

## Files

| File | Purpose |
|------|---------|
| `Scripts/Runtime/Characters/Direction8.cs` | 8-direction enum and turning a vector into a direction |
| `Scripts/Runtime/Characters/ICharacterMotionSource.cs` | The "who moves the character" interface |
| `Scripts/Runtime/Characters/CharacterAnimationSet.cs` | ScriptableObject with animation frames (`DirectionalClip`) |
| `Scripts/Runtime/Characters/CharacterAnimator.cs` | Shows the right frame for the movement and direction |
| `Scripts/Runtime/World/ClickMarker.cs` | Click marker |
| `Scripts/Runtime/Player/PlayerMovement.cs` | Now implements `ICharacterMotionSource`; fixed stuck check |
| `Scripts/Editor/ArtImportRules.cs` | Automatic art import settings |
| `Scripts/Editor/TestSceneBuilder.cs` | Formerly `Stage1SceneBuilder`, now with trees, animations and the marker |
| `Scripts/Editor/PlaceholderArtGenerator.cs` | Generates the character frames, the tree and the marker ring |
| `Data/Characters/Player_AnimationSet.asset` | The player's animation set |
| `Art/Placeholder/Player/*.png` | 30 temporary character frames: 5 directions × (2 Idle + 4 Walk) |
| `Prefabs/World/Tree.prefab` | Tree |
| `Tests/PlayMode/CharacterAnimationTests.cs`, `ClickMarkerTests.cs` | New tests |

All paths are inside `Assets/_Project/`.

## How to check

### By hand
1. If the scene is old: `RPG > Build Test Scene`.
2. Open `World_Test.unity` and press **Play**.
3. **Walk with WASD in all 8 directions** (e.g. W+D = NE). The character must face the way it moves:
   - towards the camera (S, SE, SW, E, W) the eyes and the golden buckle are visible;
   - away from the camera (N, NE, NW) only the hood is visible.
4. **Walking**, the character lifts its legs and sways; **standing**, it slowly "breathes".
5. **Click the ground.** A pulsing ring appears there and disappears when the character arrives.
6. **The tree left of the start:**
   - walk **behind** it (higher on screen) – the crown must cover the character;
   - walk **in front** of it – the character must be in front;
   - you cannot pass through the trunk, but you can pass under the crown.
7. **Console** – no red errors.

### Automatic tests
`Window > General > Test Runner` → **PlayMode** → **Run All**. **24** tests must pass:
- `CharacterAnimationTests`:
  - 10 direction cases;
  - switching between Idle and Walk;
  - west-side mirroring;
  - S direction fallback;
  - one-off animation.
- `ClickMarkerTests`: the marker is visible only while there is a target.
- `PlayerMovementTests`: now with a frame hitch test.

## How to add a real painted character

1. Paint 5 directions (E, NE, N, SE, S) for each animation. One frame is a separate PNG, or a sheet whose name ends with `_Sheet`.
2. Drop the files into `Assets/_Project/Art/Characters/<CharacterName>/`. The import rules set 256 PPU and a bottom-center pivot. If the feet are not at the very bottom of the image, drag the pivot to the feet in the Sprite Editor.
3. Create a new set: `Create > RPG > Characters > Animation Set`. For each animation enter the `id` (`Idle`, `Walk`), fps and drag the frames into the directions. Leave the west directions empty.
4. In the prefab's `CharacterAnimator` component, choose the new set.

One unit is 256 px high. A human-sized character should be about 180–200 px high, like the placeholder.

## Known limitations
- Click-to-move still walks in a straight line. Pathfinding comes in stage 5.
- The placeholder art is temporary. Real painted art has to be drawn or commissioned.
