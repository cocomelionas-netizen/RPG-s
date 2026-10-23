# World and balance from the source game's data

A personal test project: the layout and the balance numbers are taken from the source game's client data (`game.db`,
`maps/*.map`) and its client executable. The art is our own.

Generated data lives in `Scripts/Editor/*Data.cs` (`StartAreaData`, `GearData`, `AffixData`, `DungeonData`) and
`Scripts/Editor/TestSceneBuilder.Spells.cs`; the dungeon data is made by `Tools/GameData/dungeons.py`.

**Names are our own.** Every proper name of the source data (places, people, monsters, bosses, items, spells, affixes)
is replaced by this project's own through `Tools/GameData/names.py` (`WORDS`, plus `AFFIX_WORDS` for affix names): every
generator passes its texts through `names.rename`, and hand-written C# literals are rewritten with
`python3 -I Tools/GameData/names.py <files>`. The main town is St. Aurelis, the island Halvaren. Asset file names (spells,
tomes) keep their old ids so existing assets and `.meta` files stay valid; only the shown names change. Add a word to
`WORDS` and regenerate when a source name still shows in the game. The tables below use the new names.

## Map

- The world is the source game's whole island (map "fanadin": 569 × 690 cells here). Its picture keeps the source game's
  orientation: north up, St. Aurelis in the east, Karthmoor Castle in the middle, Ostrimar's Fall and Brackenmere Dale in the west,
  the beach and the Barrow Grounds in the south. A source position (x, y) is the cell (597 − y, 688 − x)
  (`Tools/GameData/island.py`: `cell_of`); dungeon plans are turned the same way. Data: `Scripts/Editor/StartAreaData.cs`
  (`Tools/GameData/island.py`).
- **Ground** – cell by cell from the source game's `maps/fanadin.map` (`Tools/GameData/world.py` →
  `Scripts/Editor/WorldMapData.cs`): its walls, fences and rock walls (flag 0x20) are rocks here, its trees and other
  blocked objects (0x60) are woods (tiles of the obstacle tilemap, not objects: a tree where the source game has one, more
  trees between them, a bush on every other cell, so every blocked cell shows that it blocks; the rocks and trees block as
  one merged static collider; see [Art.md](Art.md), "Buildings and props"), everything else is grass.
  Cells the file does not list are open ground. So the towns, the walls of St. Aurelis and its houses, the fences and the
  woods stand where the source game has them. From the start, 863 of the island's 870 NPC and object spots can be walked
  to; the rest stand in fenced pens (a few pigs), as in the source game.
- **Check against the source** – `python3 -I Tools/GameData/verify_map.py <source data> [side_by_side.png]`: for every
  zone, how much of our water/land agrees with the source game's world map picture (exits 1 under 95 %), whether each map
  label stands in its zone and each waypoint on walkable ground, and a side-by-side picture of both maps.
- **Zones** – the `.map` file ends with the zone (`zone_template`) of every 13 × 13 block (`map_file.read_zones`); they
  are the places here (`ZoneMap`): the minimap and the "Entering ..." banner show them. Its "Karthmoor" is spelled
  "Karthmoor".
- **Start** – Wayfarer's Haven (its `start` teleport 17, 106): new characters appear here, and after death.
- **Starting gear** – like the source game's `player_create_item`, a new character starts with its class's plain
  level 1 weapon in hand: the warrior a Copper Hewer and a Barricade shield, the ranger a Shortbow, the mage a Stave
  (the builder picks the cheapest level 1 source piece of the class), and everyone the source's starting clothes
  (item_template 13-16: Shirt, Pants, Shoes, Handwraps; cloth anyone may wear, 1 armor each, at the catalog's end,
  `Data/Items/Starter`). `Inventory.startingGear`; `CharacterPersistence` puts it on. Characters saved before keep
  what they have.
- **Main town** – St. Aurelis (524, 250).
- **Vendors** – every vendor of the island where the source game stands it (see "Vendors" below).
- **Monsters** – all 531 hostile NPCs of the island at their spots, with their levels (1–30) and respawn times (up to
  10 min). HP and damage grow with the level (`MonsterAI.HealthScaleBetween`); its elites and bosses are elites here.
  A spot's monsters exist only while a player is within 22 world units (`MonsterSpawner.ActivationRange`); the idle
  ones go away 5 s after every player has left, so the island only runs the monsters around its players.
- **Chests** – 197 at their spots, with their levels and respawn times.
- **Waypoints** – at the source game's 9 waypoints: Wayfarer's Haven, St. Aurelis, Karthmoor Beach, Fields of Slumber,
  Copse of the Drifter, Trader's Road, Ostrimar's Fall, Turncoats, Ruins of Ostrimar. Walking up to one discovers it
  ("You've discovered a new waypoint!"); from any of them you teleport to a discovered one **for free**, as in the source
  game ("Teleport to this waypoint?").
- **Dungeon entrances** – where the source game has them (see "Dungeons" below).
- **M** – the map of the whole island like the source game's: its places with their level ranges where its map has
  them (Gloomhound's Valley Level 5+, Grulk Cave Level 3+, ... Emberreach Level 25 PvP (Boss); from `mapquester_map.png`,
  pixel = (614 + 0.855 (x − y), 16 + 0.4275 (x + y))), St. Aurelis in large letters, the waypoints (greyed until
  discovered), the dungeon entrances, other players and you. The island is our own painting of its ground
  (`Tools/Art/worldmap.py` → `Resources/UI/Map/World.png`: meadows with a soft relief, woods, lakes, walls, a ragged
  coast and the sea), also under the minimap. Every marker is a round badge (town, area, dungeon, elite/boss,
  waystone, each kind of merchant, trainer, townsfolk, a pin, you as an arrow the way you walk); the elites and bosses
  out in the world come from `npc_template.bool_elite`/`bool_boss` (`island.py` → `StartAreaData.Bosses`, one badge per
  group of the same name). The mouse wheel zooms in on the pointer (merchants from 2x, townsfolk and boss names from
  3x), dragging moves the map, + / − / Find Me buttons, and the pointer over a badge tells what it is (a dungeon: who
  may enter, its key and bosses).

## Vendors

From `npc`, `npc_template`, `npc_vendor` and `npc_vendor_random` (`Tools/GameData/world.py` → `Scripts/Editor/VendorData.cs`):
25 vendors and the transmuter with their names, subnames and spots; 21 vendors sell something that exists here and stand in
the world, with Mortimer.

| Who | Where | Sells |
|---|---|---|
| Maelis, Innkeeper | St. Aurelis | Life and Mana Potion, Fresh Apple Pie, **Scroll of Returning** |
| Faelan, Alchemist | St. Aurelis | every potion and serum |
| Tamsin, Gems | St. Aurelis | Ruby, Emerald, Topaz, Sapphire, Amethyst, 3 of each, back every 60 s (its orbs and Rough gems do not exist as stock here) |
| Brannoc, Butcher | St. Aurelis | Gourmet Ham Sandwich, Oven Baked Turkey, Roasted Pork |
| Gilbert, Banker | St. Aurelis | Major Vault Gold, Vault Gold, Vault Coins |
| Osric (Paladin), Sigrun (Ranger), Ithrin (Mage) | St. Aurelis | their class's Tomes |
| Mortimer, Transmutation | St. Aurelis | nothing: the TRANSMUTE window ([Crafting.md](Crafting.md)) |
| Bram (Paymaster), Edric (Soldier), Anton (Explorer) | on the way | low Tomes, Minor potions, Military Ration |
| Morwen, Wenna, Greta, Aldous | around | Minor Life and Mana Potions |
| Otto (Heavy Armor), Willa (Cloth Armor), Zarrimon (Blades), Bertram (Axe & Mace), Liora (Ranged & Shield), Vasil (Staves and Wands) | St. Aurelis | gear of their kind for every second level: chain and plate / leather and robes (head, chest, feet), swords (also for axes and maces: the warrior's weapon here), bows, staves |

- The gear merchants hold one of each piece; at its restock (every 5 min, each piece on its own clock) a piece gets a
  **random quality** with the source game's chances (green 100 %, blue 50 %, gold 5 %, purple 1 %, best first), shown
  with its affix and a higher price.
- **Restocks**: every stock item has its own time (`VendorDefinition.restockTimes`): the source game's `restock_cooldown`
  (60 s for most, Nibbets' food and potions 30 s and Dragonforged Treasure 5 min), **5 minutes for orbs and gems** (the
  gem trader's Divine, Major, Greater... orbs and gems; slower than the source game's minute) and for the merchants'
  gear. The vendor window says when a sold-out item is back ("back in 2:05") and asks the server again every 3 s while
  open. What is left and when each item restocks are saved in real time (`VendorSaves`: PlayerPrefs in a browser, JSON
  files on a computer), so logging out or closing the game stops no clock: an item whose time came meanwhile is restocked
  when the world starts again, and keeps its rhythm.
- Prices: the source game's buy price (sell price × its buy cost ratio), e.g. Scroll of Returning 300, Minor Life Potion 15,
  Ruby 1500, Vault Gold 39920; vendors pay the source sell price (100, 5, 250, 499).
- Not placed: Garrand (Cleric) only sells Cleric Tomes, Ishan (Jeweler) only necklaces and rings – neither exists here.
- Vendors are dressed like the townsfolk below, after their source model.

## Townsfolk and gossip

From `npc`, `npc_template`, `npc_models`, `gossip`, `gossip_option`, `scripts`, `world_texts` and `spell_template`
(`Tools/GameData/townsfolk.py` → `Scripts/Editor/TownsfolkData.cs`, `Data/World/GossipCatalog.asset`):

- **Townsfolk:** every friendly NPC of the island (faction 1) that is not a vendor – 33 of them in 38 spots (the Guard
  stands in 6): guards (Fergus, Torvald, Benedek, Cassian, Gareth, Royce), Halvard at the start, Aldric, Velmorian The Pure,
  the town council, Breda the brewer, Grizzik the arena master, Rennick the town crier... Horses, pigs and boars are left
  out (no art). Each has its name and subname over its head (green; vendors are gold).
- **Looks:** dressed in Flare's hero layers after the source model: `knight` in plate with a longsword and kite shield,
  `LuxHighPriestNpc` and the old men in robes and hoods, `OldWitch` in robes with a wand, nobles in robes, peasants and
  traders in cloth and leather (`FemalePeasant*` are women); `peddler_goblin` (Grizzik, Nibbets) is Flare's goblin.
- **Gossip window** (click a townsperson): its menu's text and replies. A menu has several texts; the first whose
  conditions are met is said. Conditions (type, value, true/false): 1 has the quest, 2 turned it in, 3 has it finished in
  the log, 7 has a spell's aura, 12 has or did the quest, 15 level above. Quests do not exist yet (they count as never
  taken); a quest log will answer them through `IQuestFacts`.
- **Placeholders:** `$PLAYER$` / `$N` (name), `$NPC$`, `$class$` / `$CLASS$` (class, lower case / as written),
  `$G=his:her$` (man / woman). Narration in angle brackets (`<Aldric hesitates.>`) is shown in grey italics. The source
  game's own name in a text becomes "the island".
- **Replies** open another menu (or close the window) and may run a script (checked by the server: the player stands at
  the NPC, the reply belongs to its menus, its conditions are met). Script commands: 1 cast a spell on the player (Drunk
  from Breda's beer: an aura of 10 s, with no effect here – "You've had enough" while it lasts), 2 talk credit for an
  NPC (for quests), 3 the NPC says a world text, 10 points the way to an NPC (the guards' "I need directions.": marked on
  the minimap – at its edge while far – and the world map, and named with its heading in the messages). Replies whose
  scripts open the bank (11), respec (12) or the arena (13), or Mortimer's "item transmutation" (−2), are hidden until
  those exist.

## Scroll of Returning and food

- **Scroll of Returning** (`item_template` 1126, spell 282): 2 s cast standing still, then you are in St. Aurelis (the source
  game: the nearest waypoint); moving or being stunned breaks it off, and only a finished scroll is used up.
  5 min cooldown, stacks to 5. Sold by Maelis. It can be put on the action bar (the bar shows "Returning" while reading).
- **Food** (Military Ration, Fresh Apple Pie, Gourmet Ham Sandwich, Oven Baked Turkey, Roasted Pork): health and mana at
  once (the source's 25–400), not within 5 s of taking damage, 30 s cooldown of its own.

## Balance

What these numbers make of a fight, a level and an hour of play, level by level: [Balance.md](Balance.md)
(`Tools/Balance/model.py`).

| What | Source | Where |
|---|---|---|
| XP per level (1→2: 100, 10→11: 1087, 24→25: 97 380) and per kill (level 1: 20 … 25: 2389) | `player_exp_levels` | `LevelCurve` |
| Normal monster HP: 60 at level 1 (Mandiblet), 7340 at 25 (Korthian Brute), a power curve through both (the server works out the rest) | `npc_template` | `MonsterAI.SourceHealthAt` |
| Elite HP ×3 (Korthian Elite 22025 / Brute 7340); bosses their own HP (Vorlash 18515, Lord Valdrek 245750...) | `npc_template` | `MonsterAI.EliteHealthScale`, `DungeonData` |
| Elites: more gear rolls; boss: a guaranteed gold piece + 25 % purple | `npc_template` loot chances | `MonsterAI`, `LootDrop`, `AffixCatalog.RollQuality` |
| Vendors: Rare 1, Uncommon 3, each item restocks on its own time (60 s; orbs and gems 5 min) | `npc_vendor` | `VendorDefinition` |
| Attribute prices in XP | client executable | `AttributeRules.PointCost` |
| Class attributes and HP/mana at level 1 (warrior 75/30, ranger 60/45, mage 40/70; attributes and gear add more) | `player_class_stats` | `CharacterClassDefinition` |
| HP and mana grow × level, from level 21 another ×1.5 | `player_class_stats` | `LevelCurve.HealthGrowth` |
| Monster damage grows like player HP (a monster of your level is as dangerous at 1 as at 25) | – | `MonsterAI.DamageScaleBetween` |
| Player damage (weapons, abilities, spell damage, damage over time) × 2 at level 1 … 16.5 at 25, so a fight with a monster of your level lasts about 10–15 s of auto-attacks against the source game's monster health (its server formulas are not in the client; see [Balance.md](Balance.md)) | – | `LevelCurve.DamageGrowth` |
| Spells: the source game's formulas (`effect*_scale_formula`): healing, shields and mana as they are; damage half the formula × the damage growth | `spell_template` | `AbilityDefinition.SpellPowerAt`, `SpellDamageScale` |
| Prices: vendors ask the source game's buy price (sell price × buy cost ratio: potions and gear ×3, gems ×6, Tomes × their level) and pay its sell price | `item_template` | `ItemDefinition.price`, `sellValue` |
| Monster gold by its level: 3.2–4.8 × level (3–5 at level 1, 32–48 at 10, 80–120 at 25), whatever it is; elites × 3 | – | `MonsterAI.GoldAt` |
| Affixes ("of the Novice", "of Magical Vim"...) with their level ranges and stats | `affix_template` | `AffixData`, `AffixCatalog` |
| Potions: Minor/Lesser/–/Greater/Major Life and Mana Potion (levels 1, 5, 12, 18, 24, over 20 s) and Serum (at once), a shared 60 s cooldown, 5 → 1 at the transmuter | `item_template`, `spell_template`, `crafting_recipes` | `TestSceneBuilder.CreatePotions`, `Inventory` |

The default drop chances of ordinary monsters live on the source game's server, so they are neither in `game.db` nor in
the client – we use our own, stepped like its bosses'.

## Items by level

- **Equipment slots** like the source game's: Weapon, Helm, Chest, Feet, Necklace, Belt, Legs, Hands, two Rings and the
  Offhand (shield) (`Inventory.EquipmentSlots`). As in the source game they are on the character sheet (C): the GENERAL,
  COMBAT and SKILLS tabs, the worn gear down both sides (click to take off, drag from the bag to put on), name, level,
  progression and experience between them, the stats on the right. Laid out like the source game's panel: Helm, Necklace,
  Chest, Ranged (shows a worn bow), Offhand, Weapon on the left; two Rings, Hands, Belt, Legs, Feet on the right. SPEND EXP
  starts editing: + buttons add pending points (purple), COST shows their price, CONFIRM buys them, CANCEL drops them, and
  the ABILITIES window (P: SPELLS and ACTIONS tabs, a purple + raises a spell's rank) opens beside it. Each class trains
  only its own weapon skills (`CharacterClassDefinition.weaponSkills`: warrior Maces/Axes/Swords/Shields, ranger
  Ranged/Daggers, mage Staves/Wands); the others show red with no +, and the server refuses them. The inventory (I) is
  only the bag.
- **Class gear** like the source game's: swords for the warrior, bows for the ranger, staves for the mage; armor by the
  source's class armor (`player_desirable_armor`): warrior (its paladin) leather, chain, plate and the shield; ranger
  leather and chain; mage robes (`ItemDefinition.classMask`). Necklaces, belts and rings for everyone.
- **Gear names and prices** are the source game's plain pieces (`item_template`, generated; `Tools/GameData/gear.py` →
  `Scripts/Editor/GearSourceData.cs`): Bronze Hewer, Aspen Shortbow, Aspen Stave, Leather Trousers, Bronze Grips,
  Pendant, Girdle, Ring, Barricade... Legs (80 % of a chest's armor) and hands (50 %) in every armor type; a necklace, belt
  and ring for every level, always with an affix; the warrior's shield (Barricade) for every level.
- **Sockets** follow the quality, like the source game's `num_sockets`: plain none, green 0–1, blue 0–2, gold 1–3, purple
  2–4 (`LootDrop.RollSockets`). Gems go into the first empty socket and all of them count.
- Every weapon and armor piece has a level (`ItemDefinition.itemLevel`, the source's `required_level`):
  it can only be worn from that level; the description says "Requires level N".
- **Gear drops finished**, as in the source game: monsters and chests drop pieces **by their level**, the material picked
  the way the source game picks it (`material_chance_*`): from 3 levels below to 1 above, most often its own level
  (`LootDrop.LevelWeight`, `LootDrop.PickGear`). E.g. a level 1 monster – Bronze Sword, Aspen Bow, Leather Cap, Linen
  Hood; Mythril only from level 6. A piece may have a random affix (green or better) and the sockets of its quality.
- A piece drops with a 20 % chance per roll (the source game keeps its drop chances on its server); 60 % of the pieces are
  picked among what the class of one of the killers can use (`LootDrop.ClassGearShare`), the rest is any gear (to sell). Elites roll twice + 3
  extra rolls, bosses + 6 extra and a guaranteed gold piece (+25 % purple). Chests: see "Chests" below.
- The gear merchants of St. Aurelis sell the same pieces with random qualities (see "Vendors"). There is no gear
  crafting; Mortimer transmutes potions, gems, orbs and keys ([Crafting.md](Crafting.md)).
  Warrior – swords, leather, chain, plate; ranger – bows, leather, chain; mage – staves, robes
  (the source's `player_desirable_armor`).
- Monster and chest levels come from the source data (every spot has its own level).
- **Gear** (`Scripts/Editor/GearData.cs`): for every level 1–25 – the materials that
  `material_chance_weapon` / `material_chance_armor` allow at that level, with the same chance
  (`ItemDefinition.dropWeight`). Material names come from the client executable:
  - weapons: a sword for the warrior (Bronze, Steel, Mythril, Emerald, Crystal, Diamond, Titanium), a bow for the ranger and
    a staff for the mage (Aspen, Elm, Cherry, Black Walnut, White Oak, Hard Maple, Hickory);
  - armor (head, chest, feet), for everyone: leather (Leather, Brigandine, Gambeson – Cap/Jerkin/Boots),
    chain (Bronze, Steel, Mythril, Emerald – Coif/Chainmail/Greaves), plate (Crystal, Diamond, Titanium –
    Helmet/Breastplate/Sabatons), robes (Linen, Wool, Silk, Spiritsilk – Hood/Vestments/Slippers, give mana);
  - damage and armor grow with the source sell price (weapon 50 + 10 × level + 2 × level × (tier − 1),
    chest 50 + 2.5 × tier × level). E.g. a level 1 Bronze Sword – +2 damage, a level 25 Titanium Sword – +21.
  - The former crafting materials (Ingot, Log, Hide, Cloth) no longer drop; they only sell.
  - The old hand-made items stay only with vendors (the rare ones) and in old saves.
- Colours as in the source game: green → blue → **gold** (Epic) → **purple** (Legendary, the top).
  A boss – a guaranteed gold piece + 25 % purple.

## Stats (C window: GENERAL, COMBAT, SKILLS)

All of the source game's stats (names, ids and XP prices from the client executable); points can be bought with XP for each:

| Page | Stats | What they do here |
|---|---|---|
| Attributes | Health, Mana, Strength, Agility, Willpower, Intelligence, Courage, Regeneration, Meditate | Willpower +0.5 % ability damage and healing; Regeneration / Meditate – 1 HP / mana every 5 s per point, also in combat |
| Combat | Melee / Ranged / Spell Critical, Dodge, Block, Resist Frost / Fire / Shadow / Holy, Bartering, Lockpicking | criticals +0.2 % per point; Dodge +0.2 % (up to 40 %) to avoid a hit; Block +0.2 % (up to 50 %) – half damage; resist r / (r + 100); Bartering −0.5 % buying / +0.5 % selling (up to 25 %); Lockpicking – for chests (2 × level) |
| Weapon Skills | Staves, Maces, Axes, Swords, Ranged, Daggers, Wands, Shields | +0.5 % damage with a worn weapon of that kind; Shields +0.1 % block |

Prices: criticals (melee, spell) x/2 + 1, ranged criticals and resists x/3 + 1, Dodge / Block x + 1, Bartering /
Lockpicking 2x + 1, Regeneration / Meditate x^1.4 + 4, weapon skills 3 × the Strength price.

From level 3, monsters at every fourth spot (and elites) hit with magic – Frost, Fire, Shadow or Holy (numbers in that
colour), so resists matter.

## Affixes

- All 7145 of the source game's affixes (all suffixes: "Steel Sword of the Novice", "Aspen Bow of Magical Vim"), with their
  level ranges and all their stats (up to 5 in one). Kept inside `AffixCatalog.asset`.
- The source's affix value is a multiplier (0.3–3.0) that its server turns into a number by level (the formula is not in the client).
  Here: stat = multiplier × (level + 2) / 2 at the middle of the affix's levels (Novice, levels 1–5: +5; Elder, 20–25: +34),
  HP and mana 3 times that, Weapon Value adds to damage.
- Quality by the number of stats: 1 – green, 2 – blue, 3 – gold, 4+ or level 25 – purple.
- Our old affixes stay on items that already have them, but no longer drop.

## Loot

- **Monster names** are the source game's (Timber Gloomhound, Mandiblet, Rotsworn Guardsman...): every spot of the world
  and every dungeon monster (`Tools/GameData/monsters.py` → `Scripts/Editor/MonsterData.cs`).
- **Junk** like the source game's `npc_models_junkloot`: each monster drops the kinds of junk of its model (a howler
  Wolf Fangs and Patches of Fur, an insect Insect Scales...), at its level: 30 kinds × 25 levels ("Outlandish Hoof" at 1 …
  "Notable Hoof" at 25), sold for the source price (1 … 49 gold). 60 % per kill (the source game rolls it on its
  server). The old Goblin Ears, Brute Tusks and Orc Fangs no longer drop.
- What the client does not have (it lives on the source game's server): the ordinary monsters' chances of gear, its
  quality and of junk, and the affix chance; ours are documented above (gear 20 % per roll, affix 60 %, junk 60 %).
  The world-loot buff scrolls (Scroll of Strength I–V...) do not exist here yet.
- Sparkles show where a monster left loot. Clicking walks up and opens a small loot window: the items with their
  descriptions; clicking an item takes it, "Take all" takes everything. An empty bag disappears. Stacks show "x10".

## Potions

- 5 tiers: Minor (level 1), Lesser (5), – (12), Greater (18), Major (24). They can only be drunk from that level;
  a potion on the action bar drinks that one, or the best of its kind once it runs out.
- A Potion heals over 20 s (every 2 s), a Serum (green) at once and more. All potions share a 60 s cooldown, as in the source game.
- Amounts are the source game's: Minor Life Potion 9 every 2 s (90), Major 400; Minor Life Serum 135, Major 600; mana 110 … 500, serums 162 … 720.
- Monsters drop potions by their level, 15 % a Serum instead of a Potion. Mortimer transmutes 5 of a kind into the next tier
  (5 Major Potions → a Major Serum).

## Chests

The source game's containers (`gameobject_template`, field names from the client executable):

| What | Locked | Inside |
|---|---|---|
| Crates and barrels (Crate, Barrel) | no | gold only (level × 2–4) |
| Chests (Chest) | yes: Lockpicking ≥ 2 × level | a piece of gear: green 100 %, blue 12 %, gold 5 %, purple 1 % instead; a potion 75 %; a gem 75 %; gold × 2 |
| Strongbox (further on in the world) | yes | 2 more guaranteed pieces, a potion and a gem 100 %, gold × 3 |

- Clicking a locked chest: with too little Lockpicking – "Lockpicking not high enough" (how much is needed and how much you have);
  with enough – 2 s of picking (Pick Lock), then the loot window opens with what is inside. Walking away breaks it off.
- Above a locked chest it says "Locked (Lockpicking N)". Lockpicking is raised in the C window, Combat page (2x + 1 XP).
- Chests hold finished gear like monsters do (more of it, and better).

## Spells (up to level 20)

The source game's spells (`spell_template`, the Tomes in `item_template`): the Warrior has the Paladin's spells, the Ranger the
Ranger's, the Mage the Mage's (`Scripts/Editor/TestSceneBuilder.Spells.cs`). Cooldowns, cast times, mana (base + per level), weapon
damage percentages and spell formulas (rank + (stat + stat2 × k) × coef. / (105 − rank × d)) are the source's; healing,
shields and mana as they are, damage half the formula × `LevelCurve.DamageGrowth` (see "Balance"). Ranges: source yards / 120 (melee 130 → 1.1; spells 610 → 5.1).

| Class | Spells (level) |
|---|---|
| Warrior | Mighty Blow, Holy Ire, Luminance (1), Hammer of Prowess (2), Charge (3), Benediction of Heroes (4), Divine Strike (5), Cleanse (6), Reckoning Aura (7), Benediction of Defense (8), Touch of Salvation (10) |
| Ranger | Treacherous Strike, Ensnaring Shot, Mark Target (1), Multi-Shot (2), Stunning Shot (3), Focused Evasion (4), Ambush (5), Spine Shot (6), Fade (7), Mischief (8), Aimed Shot (9), Fervor (10) |
| Mage | Fireball, Ignite, Chains of Ice (1), Sear (2), Ice Shard (3), Gift of Foresight (4), Ice Blast (5), Vim of Wisdom (6), Shackle Spirit (7), Gift of Protection (8), Kindling (9), Teleport (10) |
| Warrior 11–20 | Salvation Aura (11), Reprisal (12), Hallowed Storm (13), Bastion Aura (14), Pinions of Liberty (15), Rampart of Valor (16), Deliverance (18), Shield Block (19), Divine Protection (20) |
| Ranger 11–20 | Sleep Arrow (11), Hobble (12), Strategic Retreat (14), Spectral Sting (15), Flame Arrow (16), Explosive Arrow (18), Eye Rake (19), Arrow Barrage (20) |
| Mage 11–20 | Deep Freeze (11), Magical Empowerment (12), Wisdom of the Anointed (14), Mirage Gate (15), Remove Curse (16), Magical Muting (18), Spellward (19), Inferno (20) |

- At the start 3 level-1 spells are known. The others are taught by Tomes (price 10 × level gold) from the trainers on the
  St. Aurelis square: Osric (Warrior), Sigrun (Ranger), Ithrin (Mage) – the source game's trainers. A Tome can be read from the spell's level.
- **Ranks**: every spell has ranks 1–10, raised for XP in the spellbook (P). Price: 10 × spell level + 10 × rank^1.5
  (the formula is not in the source client – it is on its server). The rank raises damage/healing (in the formula) and effect strength.
- **Effects** (`StatusEffects`): stun, root, slow, damage and healing over time, more/less damage taken or dealt,
  attribute and Intelligence buffs, spell critical, dodge, "Evade", a shield (Absorb), "Incapacitate" (any damage breaks it),
  invisibility to monsters (Fade), mana when it runs out (Fervor), stun immunity. Icons are under the player and target
  bars (green frame – buff, red – debuff).
- **Bar**: 12 slots like the source game's (keys 1–9, 0, -, =): Attack, spells and items, arranged by dragging
  (see [Crafting.md](Crafting.md)). A bar shows while casting; moving breaks the cast.
- Second-batch effects: Pacify (cannot attack), haste, invulnerability, mana burn, less healing, faster attacks,
  block rating, Silence (cannot cast). Deliverance revives the nearest fallen friend on the spot
  (40 % HP, 20 % mana); Mirage Gate – takes you to St. Aurelis after 10 s; Strategic Retreat – a leap back from the enemy.
- Simplified: Charge – straight next to the target; Reprisal always works (not only after a parry); Eye Rake – less damage;
  Spellward – Silence; Remove Curse – like Cleanse; Multi-Shot – 3 × 70 %; Ice Shard only works on slowed targets (Chains of Ice),
  Spine Shot only on "Incapacitate"; Mischief only on monsters not yet in combat; missing durations were chosen.


## Later items, monster traits, Sleep

- **Orbs**: every tier of the source game's Accessory, Armor and Weapon orbs (Minor, Lesser, Greater, Major, Divine,
  Arcane; `item_template` 924-935, 1128-1130, 1140-1142) with its sell price and buy cost ratio. They are transmutation
  ingredients, and used on gear (click the orb, then the item, like a gem) they **empower** it, as the source game's
  ApplyOrbEnchant (spells 129-140: power Minor 2 ... Divine 6, gear mask by orb kind): Weapon orbs on weapons and
  shields, Armor orbs on head/chest/legs/feet/hands, Accessory orbs on necklaces, belts and rings. Each try uses one of
  the item's empowerment points (its quality: white 1 ... orange 5); it works with 30% + 12% per power (Divine always)
  and then adds a step: +1 item level and 10% more of the item's damage or armor (accessories +1 to each main
  attribute). Arcane orbs always work, need no point, on gear whose points are used up, up to 3 steps beyond. The
  source game keeps the exact chance and gain on its server, so these numbers are this project's.
- **Attribute scrolls** I-V of Strength, Agility, Intellect, Willpower and Courage (1085-1109, spells 246-270): +4, 7,
  11, 14 and 21 of the attribute in the source game (twice that here, like the gems) for 5 minutes, levels 4/8/14/18/23,
  not in combat, one of each attribute at a time (`ItemUse.Buff`). Vendors that stock them sell them; Morvagos drops the V
  scrolls.
- **Gems**: the source game's Flawless (+4, twice here) and Dragonforged (+5) tiers and Dragonforged Treasure (the wyrms' 1%
  drop, `DungeonData.OutdoorLoot`). The dungeons' "Refined" drops are this project's Refined tier, their "Flawless"
  drops the new Flawless gems.
- These items are appended after the Tomes (`TestSceneBuilder.LaterItems.cs`) so saved item ids stay, and so are the level 1
  Tomes the trainers list (reading one you know does nothing).
- **Monsters** (`Tools/GameData/monsters.py` → `MonsterData`): neutral kinds (`npc_template.faction` 2: Mandiblet, Timber
  Gloomhound, Gorgonet...) fight only when attacked; spots whose `npc.movement_type` is 0 stand still; monsters with a
  `mechanic_immune_mask` (most bosses) are never stunned, incapacitated or rooted.
- **Monster spells** (`Tools/GameData/monster_spells.py` → `MonsterSpellData`, `TestSceneBuilder.MonsterSpells.cs`,
  `Monsters/MonsterCaster.cs`): 105 monsters cast the source game's spells (npc_template `spell_primary`, `spell_1..4`
  with chance, try interval and cooldown): bolts (Frost Bolt slows, Storm Bolt stuns, Shadow Bolt makes you take more
  damage), strikes (Hobble, Mortal Wound halves healing), damage over time (Rend, Blight, Venomous Bite), fears
  (Howl of Terror), War Stomp, enrages, heals of hurt allies, charges, and the bosses' volleys, Unmake, Finger of
  Death and Forceprison. A primary spell (casters, archers) is cast from its range instead of swinging. Casting stands
  still and is broken by stuns and silences; everyone sees the spell's name and its bolt or ring. Fixed source amounts
  are measured against an ordinary elite's hit (400); the others scale with the monster's own hit. Bosses with spells no
  longer use the generic ground slam.
- **Sleep** (spell 245): every class knows it: a 2.5 sec cast out of combat, then 40% of health and mana; 3 min cooldown.

## Dungeons

All of the source game's dungeons that are entered from our part of the world (and from inside them), generated by
`Tools/GameData/dungeons.py` → `Scripts/Editor/DungeonData.cs` (the `.map` format is in `Tools/GameData/map_file.py`):

- **Plan** – from `maps/<map>.map` (cell flag 0 – walkable, 0x20 / 0x60 – wall): the rooms, corridors and
  pillars are the same. Arrival, exits, monsters (`npc`) and containers (`gameobject`) stand at their spots.
- **Entry rules** – `dungeon_template`: level, key (one is used up, only the opener's), number of players.
- Every group gets its own copy; monsters and chests do not come back; exits are always open.

| Dungeon (source map) | Entrance | Rules | Bosses | What the bosses drop |
|---|---|---|---|---|
| Wolf's Lair (`wolf_cave_1`) | Wolf's Lair | open | – (Cave Gloomhound, Risen Miner, level 5) | – |
| Grulk Cave (`goblin_cave`) | Grulk Cave | open | – (Cave Grulk, level 3) | – |
| The Sunken Garrison (`stormrock_ruins`) | Ostrimar's Fall | level 25, up to 4 | Thalgor (28) | **10 Tower Keys**, 3 Divine Accessory Orbs |
| Crumbling Tower (`fort_amir`) | Ostrimar's Fall | level 25, **Tower Key**, up to 4 | Lord Valdrek (30), Xalthuun (30) | Divine orbs, 4 Magic Dust |
| Forsaken Barracks (`fort_nasu`) | Karthmoor Castle | level 25, **Keys to the Barracks**, alone | Vorlash the Betrayer (25) | **Spent Key** |
| Karthmoor Underhalls (`dilapidated_sewers`) | Ostrimar's Fall | level 25, **Underhalls Key**, alone | Knight Valerian (28) | **2 Keys to St. Aurelis**, Divine Armor Orb |
| Defiled Altar (`st_maria_1`) | by St. Aurelis | level 25, **Keys to St. Aurelis**, alone | Nightbane (30) | **Corvalis's Key**, orbs, Magic Dust |
| Corvalis's Descent (`temple_of_mez_1`) | inside the Defiled Altar | level 25, **Corvalis's Key**, alone | Corvalis (30), Morvagos (30) | Major orbs, Divine Weapon Orb, gems |
| Mirethane's Netherplane (`wizards_tower_2`) | Karthmoor Outerward | level 25, **Netherplane Key**, up to 4 | Netherplanar Guardian (30) | **20 Sanctum Keys**, Flawless and plain gems |
| Mirethane's Sanctuary (`st_maria_3`) | inside the Netherplane | level 25, **Sanctum Key**, up to 4 | Prince Mirethane (30) | 5 of each Flawless gem, Arcane Essence |


**The key chain:**

- Alone: Horned Charger / Horned Beast / Lesser Basilisk (5 %) → *Keys to the Barracks* → Vorlash → *Spent Key*;
  + a *Dale Gem* (Korthian camp, 15 %) – clicking combines them into an *Underhalls Key* → Knight Valerian → *Keys to St. Aurelis*
  → Nightbane → *Corvalis's Key* → Corvalis's Descent.
- Group: Sunken Garrison (no key) → Thalgor → *Tower Key* → Crumbling Tower.
- Group: Gorvanth / Swifthorn (100 %) → *Dormant Netherplane Key* + *Magic Dust* + *Major Vault Gold* (jeweler) – clicking
  combines them into a *Netherplane Key* → Netherplanar Guardian → *Sanctum Key* → Mirethane's Sanctuary.
- The Korthian camp and Gorvanth / Swifthorn stand where the source game has them (Brackenmere Dale, the west).

**Difficulty.** Levels are the source's (bosses 28–30; monster levels can go up to 30). The source's elites and "boss"-flagged
monsters are elites here. Where the source gives a monster's HP, it becomes a multiple of a normal monster of that level
(a normal level 25 monster there has 7340 HP): e.g. Thalgor ≈ 16.5×, Lord Valdrek ≈ 29×, Netherplanar Guardian ≈ 81×, Fallen
Guard ≈ 2.7×. Damage follows its `weapon_value` (400 – an ordinary elite): Karthmoor Knight 7.5×, Guardian 11×.
Gold – `custom_gold_ratio`. Bosses have the ground slam (our mechanic).

**Chests** – the source game's containers at their spots, with their names and levels: Rosewood Crate (gold), Rosewood Chest
(locked, Lockpicking 50; a piece of gear, potion 75 %, gem 75 %), Platinum Strongbox (2 more pieces, potion and
gem 100 %, gold × 3), in the caves Elm / Aspen / Copper.

**Boss loot** – the source's `loot` tables (identical rows count once) and their quality chances (`loot_blue/gold/
purple_chance`, e.g. Lord Valdrek – purple 100 %). Orbs, Magic Dust, Arcane Essence can only be sold for now (the source
price); the source's amethysts and stat scrolls are left out (we have none); Refined → our plain gem, Flawless → Flawless gem.

## Buffs and debuffs on the unit frames

From `spell_template.aura_description` (`Tools/GameData/auras.py` → `Scripts/Editor/AuraData.cs`, 140 texts by spell name),
put by the scene builder into `Resources/StatusInfo.asset` (`StatusInfo`) with icons: a spell's or item's own icon,
else one for the kind of effect (stun, root, slow, damage over time by school: bleeding, frost, fire, shadow, holy...).
The player's and the target's frames show each effect as an icon (red frame: debuff, green: buff) with its seconds
left and stacks; the pointer over one shows its name, Buff or Debuff, the source text ("Reduces fire resistance."),
what it does with its numbers ("Takes 45 fire damage over its duration.", "Moves 30% slower."), stacks and time left.
