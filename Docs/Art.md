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

- **Ground**: Flare's grassland: 16 grass tiles, 12 paved tiles (the squares of St. Aurelis and the start, dungeon
  floors), 16 water tiles (lakes), rock pillars (the source map's rock walls, dungeon walls), bushes and broad
  leaves (Plant0, 1, 4, 5, 8, 9) on every blocked cell of the woods without a tree and on invisible walls, thin reeds
  and tall grass (Plant6, 7, 10-13) here and there on open ground (no collider), 12 trees. Bushes always block, so open
  ground never shows one.
- **Lakes**: the source map has no water; `Tools/GameData/world.py` takes it from the source game's world map picture
  (blue there, not walkable in the `.map` file, groups of 30 cells or more). Where a lake would cut off a spot players
  can reach in the source game, the fewest water cells to it are opened, three wide (its fords).
- **Containers**: Flare's dungeon crate, barrel, chest and spiked chest draw the wooden crates (barrels in dungeons),
  the locked chests and the strongboxes, a little larger than the world's 2/3 scale so they are easy to spot.
- **Heroes** (8 directions: idle, run, attack): Flare's male avatar layers drawn together in Flare's layer order per
  direction: the warrior in plate with longsword and kite shield, the ranger in leather with a longbow (attack: shoot),
  the mage in robes with a staff (attack: cast).
- **Hero layers** (`Tools/Art/avatar.py <flare-game checkout>` → `Art/Flare/Avatar/<Gender>_<slot>.png` and
  `Scripts/Editor/FlareAvatarData.cs`): Flare's male and female hero drawn in layers (head, chest, legs, feet, hands,
  weapon, shield) in Flare's order per direction (`engine/hero_layers.txt`). Kept looks per gender: the hair, the leather
  hood, chain coif, plate helm and mage hood; the bare body and the cloth, leather, chain, plate and mage pieces of
  chest, legs, feet and hands; dagger, short and long sword, battle axe, mace, short and long bow, wand, staff; buckler
  and kite shield. Armor has Idle, Walk, Swing, Shoot and Cast; a weapon Idle, Walk and its own attack. The builder
  (`TestSceneBuilder.Avatar.cs`) makes one animation set per look into `AvatarCatalog` and gives every piece of gear its
  look (`ItemDefinition.appearance`: armor by kind and slot, weapons by type and level, shields by level; a cloth hat
  leaves the hair). In the game `HeroAppearance` draws the player's layers from the worn gear and the gender chosen
  with a new character (saved with it), plays the weapon's attack (bows shoot, staves and wands cast, the rest swing)
  and builds the portrait from the layers. The three put-together heroes above stay as the classes' looks for players
  without the layers (tests).
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

Windows, buttons, item slots, tooltips and bars use **RPG UI 1** by System G6 / Qoma (CC0,
https://opengameart.org/content/rpg-ui-1), cut by `Tools/Art/ui.py <extracted rpg_ui folder>` into
`Assets/_Project/Resources/UI/Rpg` (the scene builder sets their nine-slice borders in `ImportUiFrames`):

- windows: grey stone with a steel frame (`UIFactory.AddFrame`); what NPCs say is on parchment with dark ink
  (`UIFactory.AddParchment`, the gossip window);
- buttons: steel, golden under the pointer (`UIFactory.CreateButton`); X, +, -, < and > are square steel buttons;
- item slots: a steel frame over the slot's colour (`UIFactory.MakeSlot`); empty equipment slots show a silhouette
  (helm, chest, feet...);
- tooltips and list rows (vendor goods, waypoints, replies): a dark box with a thin steel frame (`UIFactory.StyleBox`);
- bars (health, mana, experience...): a steel frame around a shaded fill of the bar's colour (`UIFactory.CreateBar`).

Without that art the older look is used: windows get an ornate gold frame (border only) and coloured labelled buttons a cut-corner shape (slots and list rows stay square) from Kenney's **Fantasy UI Borders** (CC0,
`Assets/_Project/Resources/UI`, credits there), tinted in code (`UIFactory.AddFrame`, `UIFactory.CreateButton`); the
builder sets their nine-slice borders (`ImportUiFrames`).

## World map (M)

`Tools/Art/worldmap.py <game-icons.net SVG folder>` (numpy, scipy, Pillow, cairosvg) paints `Resources/UI/Map/World.png`
(2048 × 1024, exactly the island's world rectangle) from `WorldMapData.cs` and draws the map's round badges
(`Resources/UI/Map/<Kind>.png`, 64 px: a coloured disc in a golden rim with a white glyph from game-icons.net, CC-BY 3.0,
credited in `Resources/UI/CREDITS.md`; `Player.png` an arrow, `Pin.png` a marker pin). The builder imports the picture as
a mipmapped texture (`ImportWorldMapPicture`) and the badges as sprites (`ImportUiFrames`).

## Lakes

`Tools/Art/water.py` (numpy, Pillow) paints `Assets/_Project/Art/Water/Water.png`: 256 isometric water tiles (128 × 64,
16 × 16 in the atlas), this project's own. Each lake cell is drawn on the **Water** tilemap over a grass tile, its tile
chosen by which neighbours are land (`WaterMask`: land edges 1 x + 1, 2 y + 1, 4 x − 1, 8 y − 1, plus land corners whose
two edges are water, 16 / 32 / 64 / 128): water deeper away from the shore, and toward land a bank of wet sand and foam
that fades into the grass, its corners rounded. Ripples and the shore's wobble repeat with the cell lattice, so the
tiles meet without seams. (Flare's own water tiles were only the dark lake bed.)

## Buildings and props

The source game's island map puts objects on cells (the third layer of `maps/fanadin.map`: a picture and a scale,
negative when mirrored), and the source game's `sprite_hotspot` says which pixel of each picture stands on its cell.
`Tools/GameData/props.py` reads them (`PropData.cs`):

- **Placements** – 38 buildings and ~190 props at their cells. Many of the source game's buildings are free pictures:
  rubberduck's CC0 medieval buildings, Bleed's tavern, timbered house, Galata tower and outpost (CC-BY). Those keep the
  same frame; the church, the green house, the medieval houses and the cabins get rubberduck stand-ins of the same size.
  Tents, wells, anvils, signposts, campfires, wood piles, stumps, graves and statues are Flare's.
- **WallStyles** – how each blocked cell is drawn, so that every cell that blocks shows something. Wall cells (`#`):
  nothing only where a building's or prop's opaque pixels lie over the cell's middle (read from the packed atlas, so
  `buildings.py` runs first), fences and iron fences along their line, low rubble for brick and stone wall pieces,
  rock pillars only for rock walls, ruin walls and cliffs, and bushes where the source game has only an invisible block
  (no more tall pillars hiding NPCs). Woods (`T`): `t` where the source game has a tree, `,` elsewhere; the builder
  (`PlaceTrees`) puts a tree on every source tree with no tree next to it, more trees where none is within two cells,
  and a bush on every other cell.

Run `Tools/Art/buildings.py` before `props.py`. `Tools/Art/buildings.py <downloads> <flare-game> <source data>` packs the pictures into `Art/Buildings/Buildings0.png`
(4096 × 2048, MaxRects): each building trimmed and as wide as the source game draws it (its image size there at 64
pixels a cell, kept at up to 96 pixels a cell), its pivot the source game's hotspot; Flare's props at Flare's size
(192 pixels a cell) with Flare's tile offset. Credits: `Art/Buildings/CREDITS.md`. The builder draws the buildings on a
**Buildings** tilemap (Individual, sorted with the characters, mirrored where the source game mirrors them) and the wall
styles on the obstacles tilemap (blocking; only rock pillars block sight).

## Day and night

`DayNight` (on the Global Light 2D): a day and night of 40 real minutes, from the real clock, so every player sees the
same hour (24 minutes of day, 3 of dusk, 10 of night, 3 of dawn). At night the global light dims and turns blue, as dark
as the source game makes the zone the local player stands in (`zone_template.night_pct`, `StartAreaData.ZoneNights`
from `Tools/GameData/island.py`: roads 0.05, St. Aurelis 0.1, graveyards and caves 0.45); dungeons are that dark all day.
The source game's light-giving pictures (`sprite_light`: campfires, street lights, torches, grave lanterns, fireflies;
`PropData.Lights` from `props.py`, with their colour and size) get point lights that come on at dusk, and a soft warm
light follows the local player. Unlit things stay bright: the HUD, nameplates and the glow of empowered weapons
(`RPG/SpriteGlow`). F9 → Day / night jumps this screen to the night and back.

## Music and sounds

All CC0 (credits: `Assets/_Project/Audio/CREDITS.md`), played by `GameAudio` (made by `CreateGameAudio`), and nothing of
the source game's own audio. The music and ambience follow the source game's choice for each zone (`zone_template` music
and ambience, as moods in `StartAreaData.ZoneSounds` from `Tools/GameData/island.py`): its town tune → **Town** (St.
Aurelis), its village and road tunes → **Fields**, its forest tunes → **Forest**, its grim tunes (graveyards, ruins,
castles) → **Dark**, its dungeon tunes and every dungeon → **Dungeon**; ambience: birds in fields and woods (quieter at
night), a cave's drips underground. Tracks crossfade over 2.5 s. Sounds: swings, bow shots and casts (fire spells their
own), hits (heavier on players and criticals), monster grunts and deaths, a player's death, level up, coins, picking up
loot, gems and empowerment, a failed empowerment, crafting, reading a Tome, errors, button clicks and footsteps; each
kind has a few variants (`Audio/Sfx/<Kind><n>.ogg`) and a slightly random pitch, and sounds fade with the distance from
the camera (silent beyond 14 units). The HUD's **Sound** button mutes everything (remembered on the computer).

## Weapon glow preview

`python3 -I Tools/Art/glow_preview.py out.png` draws every weapon look (dagger, short sword, long sword, battle axe,
mace, short bow, long bow, wand, staff) in the hero's hand at +0, +6, +9 and +12, the way `HeroAppearance` draws the glow
(rings of additive copies behind the weapon, the weapon, a sheen over it): a quick check after changing the glow.
