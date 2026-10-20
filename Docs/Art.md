# Art

The world, heroes and monsters are drawn with art from the open game **Flare** (CC-BY-SA 3.0; authors and license in
`Assets/_Project/Art/Flare/CREDITS.md`). UI, items, effects and a few props are still this project's own placeholders
(`PlaceholderArtGenerator`).

## How it gets in

- `Tools/Art/flare.py <flare-game checkout>` cuts what is used from Flare's `mods/fantasycore` images, scales it to this
  project's size (Flare draws a 192 × 96 tile per cell; here 128 pixels per unit, a cell is 1 × 0.5 units) and packs it
  into atlases: `Assets/_Project/Art/Flare/Tiles.png` and `Characters/<Look>.png`, plus `Scripts/Editor/FlareArtData.cs`
  (each sprite's rect and pivot — Flare's anchor: a tile's middle, a tree's or a character's feet — and the frames of
  every animation in eight directions).
- The scene builder (`TestSceneBuilder.Art.cs`) cuts the sprites from the atlases (kept inside the tile and animation
  set assets) and uses them.

## What is used

- **Ground**: Flare's grassland: 16 grass tiles, 12 paved tiles (the squares of St. Revere and the start, dungeon
  floors), 16 water tiles (lakes), rock pillars (the source map's walls and fences, dungeon walls), bushes and ferns
  between the trees, tall grass and reeds here and there on open ground (no collider), 12 trees.
- **Lakes**: the source map has no water; `Tools/GameData/world.py` takes it from the source game's world map picture
  (blue there, not walkable in the `.map` file, groups of 30 cells or more). Where a lake would cut off a spot players
  can reach in the source game, the fewest water cells to it are opened, three wide (its fords).
- **Heroes** (8 directions: idle, run, attack): Flare's male avatar layers drawn together in Flare's layer order per
  direction: the warrior in plate with longsword and kite shield, the ranger in leather with a longbow (attack: shoot),
  the mage in robes with a staff (attack: cast).
- **Monsters** (8 directions: idle, run, attack): goblin, goblin elite, skeleton, skeleton mage, skeleton archer,
  zombie, minotaur, antlion, small antlion, fire ant, ice ant, wyvern, fire wyvern. Each source-game monster gets a look
  by its name (`TestSceneBuilder.MonsterLook`: ants and bugs, wyrms and drakes, the undead, zombies and lost souls,
  horned beasts and brutes, goblin folk; anything else by level) over its level tier's numbers; health bars and hitboxes
  follow the art's height.
