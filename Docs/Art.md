# Art

The world, heroes and monsters are drawn with art from the open game **Flare** (CC-BY-SA 3.0; authors and license in
`Assets/_Project/Art/Flare/CREDITS.md`). Effects, the loot sack and a few props are still this project's own placeholders
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
- **Containers**: Flare's dungeon crate, barrel, chest and spiked chest draw the wooden crates (barrels in dungeons),
  the locked chests and the strongboxes, a little larger than the world's 2/3 scale so they are easy to spot.
- **Heroes** (8 directions: idle, run, attack): Flare's male avatar layers drawn together in Flare's layer order per
  direction: the warrior in plate with longsword and kite shield, the ranger in leather with a longbow (attack: shoot),
  the mage in robes with a staff (attack: cast).
- **Monsters** (8 directions: idle, run, attack): goblin, goblin elite, skeleton, skeleton mage, skeleton archer,
  zombie, minotaur, antlion, small antlion, fire ant, ice ant, wyvern, fire wyvern. Each source-game monster gets a look
  by its name (`TestSceneBuilder.MonsterLook`: ants and bugs, wyrms and drakes, the undead, zombies and lost souls,
  horned beasts and brutes, goblin folk; anything else by level) over its level tier's numbers; health bars and hitboxes
  follow the art's height.

## Icons

Items and spells have icons (`Assets/_Project/Art/Icons/Icons.png`, credits there), packed by
`Tools/Art/icons.py <flare-game> <painterly spell icons> <game-icons.net png archive>` with
`Scripts/Editor/IconArtData.cs`:

- **Items**: Flare's item icons: weapons by type and level (swords, bows, staves, axes, maces), the armor set of each
  armor kind (cloth, leather, chain, plate, robes) by slot, shields, rings, necklaces, belts, gems (Topaz and Amethyst
  tinted), potion and serum tiers, scrolls, Tomes (a book), keys and coins; Flare's crafting crystals for Magic Dust.
- **Spells and orbs**: J. W. Bjerk's Painterly Spell Icons (CC-BY-SA 3.0), one per spell (`TestSceneBuilder.Icons.cs`,
  `SpellIcons`); Attack is Flare's crossed swords.
- **Junk, materials and food**: game-icons.net glyphs (CC-BY 3.0) on Flare's blank stone, by a word of the name.

The builder cuts them into `Resources/Icons.asset` (`IconSet`) and sets `ItemDefinition.icon` / `iconTint` and
`AbilityDefinition.icon`. Bag, character sheet, loot, transmuter, vendor rows, the action bar, the ABILITIES window and
dragging show the icon (counts and mana costs over it); anything without one still shows its name on its color.

## UI frames

Windows get an ornate gold frame (border only) and coloured labelled buttons a cut-corner shape (slots and list rows stay square) from Kenney's **Fantasy UI Borders** (CC0,
`Assets/_Project/Resources/UI`, credits there), tinted in code (`UIFactory.AddFrame`, `UIFactory.CreateButton`); the
builder sets their nine-slice borders (`ImportUiFrames`).
