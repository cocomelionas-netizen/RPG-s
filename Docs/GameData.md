# World and balance from the source game's data

A personal test project: the layout and the balance numbers are taken from the source game's client data (`game.db`,
`maps/*.map`) and its client executable. The art is our own.

Generated data lives in `Scripts/Editor/*Data.cs` (`StartAreaData`, `GearData`, `AffixData`, `DungeonData`) and
`Scripts/Editor/TestSceneBuilder.Spells.cs`; the dungeon data is made by `Tools/GameData/dungeons.py`.

## Map

- The world is the source game's whole island (map "fanadin": 569 × 690 cells here). Its picture keeps the source game's
  orientation: north up, St. Revere in the east, Kandorys Castle in the middle, Atmar's Fall and Hillsfar Dale in the west,
  the beach and the Burial Grounds in the south. A source position (x, y) is the cell (597 − y, 688 − x)
  (`Tools/GameData/island.py`: `cell_of`); dungeon plans are turned the same way. Data: `Scripts/Editor/StartAreaData.cs`
  (`Tools/GameData/island.py`).
- **Ground** – cell by cell from the source game's `maps/fanadin.map` (`Tools/GameData/world.py` →
  `Scripts/Editor/WorldMapData.cs`): its walls, fences and rock walls (flag 0x20) are rocks here, its trees and other
  blocked objects (0x60) are trees (one drawn per little clump, every cell of it blocks), everything else is grass.
  Cells the file does not list are open ground. So the towns, the walls of St. Revere and its houses, the fences and the
  woods stand where the source game has them. From the start, 863 of the island's 870 NPC and object spots can be walked
  to; the rest stand in fenced pens (a few pigs), as in the source game.
- **Zones** – the `.map` file ends with the zone (`zone_template`) of every 13 × 13 block (`map_file.read_zones`); they
  are the places here (`ZoneMap`): the minimap and the "Entering ..." banner show them. Its "Kandoryrs" is spelled
  "Kandorys".
- **Start** – Pilgrim's Refuge (its `start` teleport 17, 106): new characters appear here, and after death.
- **Main town** – St. Revere (524, 250).
- **Vendors** – every vendor of the island where the source game stands it (see "Vendors" below).
- **Monsters** – all 531 hostile NPCs of the island at their spots, with their levels (1–30) and respawn times (up to
  10 min). HP and damage grow with the level (`MonsterAI.HealthScaleBetween`); its elites and bosses are elites here.
  A spot's monsters exist only while a player is within 22 world units (`MonsterSpawner.ActivationRange`); the idle
  ones go away 5 s after every player has left, so the island only runs the monsters around its players.
- **Chests** – 197 at their spots, with their levels and respawn times.
- **Waypoints** – at the source game's 9 waypoints: Pilgrim's Refuge, St. Revere, Kandorys Beach, Fields of Dreams,
  Thicket of the Wanderer, Merchant's Way, Atmar's Fall, Deserters, Ruins of Atmar. Walking up to one discovers it
  ("You've discovered a new waypoint!"); from any of them you teleport to a discovered one **for free**, as in the source
  game ("Teleport to this waypoint?").
- **Dungeon entrances** – where the source game has them (see "Dungeons" below).
- **M** – the map of the whole island like the source game's: its places with their level ranges where its map has
  them (Howler's Valley Level 5+, Trog Cave Level 3+, ... Fireshear Level 25 PvP (Boss); from `mapquester_map.png`,
  pixel = (614 + 0.855 (x − y), 16 + 0.4275 (x + y))), St. Revere in large letters, the waypoints (greyed until
  discovered), the dungeon entrances, other players and you.

## Vendors

From `npc`, `npc_template`, `npc_vendor` and `npc_vendor_random` (`Tools/GameData/world.py` → `Scripts/Editor/VendorData.cs`):
25 vendors and the transmuter with their names, subnames and spots; 21 vendors sell something that exists here and stand in
the world, with Reginald.

| Who | Where | Sells |
|---|---|---|
| Aedis, Innkeeper | St. Revere | Life and Mana Potion, Fresh Apple Pie, **Scroll of Returning** |
| Osraed, Alchemist | St. Revere | every potion and serum |
| Calder, Gems | St. Revere | Ruby, Emerald, Topaz, Sapphire, Amethyst, 3 of each, back every 60 s (its orbs and Rough gems do not exist as stock here) |
| Argas, Butcher | St. Revere | Gourmet Ham Sandwich, Oven Baked Turkey, Roasted Pork |
| Harold, Banker | St. Revere | Major Treasury Gold, Treasury Gold, Treasury Coins |
| Halfrid (Paladin), Kelda (Ranger), Gyldan (Mage) | St. Revere | their class's Tomes |
| Reginald, Transmutation | St. Revere | nothing: the TRANSMUTE window ([Crafting.md](Crafting.md)) |
| Emitt (Paymaster), Clive (Soldier), Stefan (Explorer) | on the way | low Tomes, Minor potions, Military Ration |
| Huldria, Helbred, Ruth, Earnest | around | Minor Life and Mana Potions |
| Karl (Heavy Armor), Robin (Cloth Armor), Hezakoth (Blades), Lowe (Axe & Mace), Selena (Ranged & Shield), Dragos (Staves and Wands) | St. Revere | gear of their kind for every second level: chain and plate / leather and robes (head, chest, feet), swords (also for axes and maces: the warrior's weapon here), bows, staves |

- The gear merchants hold one of each piece; at every restock (5 min) each piece gets a **random quality** with the source
  game's chances (green 100 %, blue 50 %, gold 5 %, purple 1 %, best first), shown with its affix and a higher price.
- Prices: the source game's buy price (sell price × its buy cost ratio), e.g. Scroll of Returning 300, Minor Life Potion 15,
  Ruby 1500, Treasury Gold 39920; vendors pay the source sell price (100, 5, 250, 499).
- Not placed: Boltar (Cleric) only sells Cleric Tomes, Vikram (Jeweler) only necklaces and rings – neither exists here.

## Scroll of Returning and food

- **Scroll of Returning** (`item_template` 1126, spell 282): 2 s cast standing still, then you are in St. Revere (the source
  game: the nearest waypoint); moving or being stunned breaks it off, and only a finished scroll is used up.
  5 min cooldown, stacks to 5. Sold by Aedis. It can be put on the action bar (the bar shows "Returning" while reading).
- **Food** (Military Ration, Fresh Apple Pie, Gourmet Ham Sandwich, Oven Baked Turkey, Roasted Pork): health and mana at
  once (the source's 25–400), not within 5 s of taking damage, 30 s cooldown of its own.

## Balance

| What | Source | Where |
|---|---|---|
| XP per level (1→2: 100, 10→11: 1087, 24→25: 97 380) and per kill (level 1: 20 … 25: 2389) | `player_exp_levels` | `LevelCurve` |
| Normal monster HP: 60 at level 1 (Antling), 7340 at 25 (Zhendale Brute), a power curve through both (the server works out the rest) | `npc_template` | `MonsterAI.SourceHealthAt` |
| Elite HP ×3 (Zhendale Elite 22025 / Brute 7340); bosses their own HP (Sarloch 18515, Lord Zargan 245750...) | `npc_template` | `MonsterAI.EliteHealthScale`, `DungeonData` |
| Elites: more gear rolls; boss: a guaranteed gold piece + 25 % purple | `npc_template` loot chances | `MonsterAI`, `LootDrop`, `AffixCatalog.RollQuality` |
| Vendors: Rare 1, Uncommon 3, restock every 60 s | `npc_vendor` | `VendorDefinition` |
| Attribute prices in XP | client executable | `AttributeRules.PointCost` |
| Class attributes and HP/mana at level 1 (warrior 75/30, ranger 60/45, mage 40/70; attributes and gear add more) | `player_class_stats` | `CharacterClassDefinition` |
| HP and mana grow × level, from level 21 another ×1.5 | `player_class_stats` | `LevelCurve.HealthGrowth` |
| Monster damage grows like player HP (a monster of your level is as dangerous at 1 as at 25) | – | `MonsterAI.DamageScaleBetween` |
| Player damage (weapons, abilities, spell damage, damage over time) × 1.5 at level 1 … 12.7 at 25, so fights last as long against the source game's monster health (its server formulas are not in the client) | – | `LevelCurve.DamageGrowth` |
| Spells: the source game's formulas (`effect*_scale_formula`): healing, shields and mana as they are; damage half the formula × the damage growth | `spell_template` | `AbilityDefinition.SpellPowerAt`, `SpellDamageScale` |
| Prices: vendors ask the source game's buy price (sell price × buy cost ratio: potions and gear ×3, gems ×6, Tomes × their level) and pay its sell price | `item_template` | `ItemDefinition.price`, `sellValue` |
| Monster gold grows with its level (goblin 3–6 at level 2, an orc 20–30 at 6, the same orc at 25: 83–125) | – | `MonsterAI.ServerOverrideLevel` |
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
  `Scripts/Editor/GearSourceData.cs`): Bronze Slicer, Aspen Shortbow, Aspen Stave, Leather Trousers, Bronze Grips,
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
- A piece drops with a 12 % chance per roll (the source game keeps its drop chances on its server). Elites roll twice + 3
  extra rolls, bosses + 6 extra and a guaranteed gold piece (+25 % purple). Chests: see "Chests" below.
- The gear merchants of St. Revere sell the same pieces with random qualities (see "Vendors"). There is no gear
  crafting; Reginald transmutes potions, gems, orbs and keys ([Crafting.md](Crafting.md)).
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

- **Monster names** are the source game's (Timber Howler, Antling, Blightchosen Guardsman...): every spot of the world
  and every dungeon monster (`Tools/GameData/monsters.py` → `Scripts/Editor/MonsterData.cs`).
- **Junk** like the source game's `npc_models_junkloot`: each monster drops the kinds of junk of its model (a howler
  Wolf Fangs and Patches of Fur, an insect Insect Scales...), at its level: 30 kinds × 25 levels ("Bizarre Hoof" at 1 …
  "Noteworthy Hoof" at 25), sold for the source price (1 … 49 gold). 60 % per kill (the source game rolls it on its
  server). The old Goblin Ears, Brute Tusks and Orc Fangs no longer drop.
- What the client does not have (it lives on the source game's server): the ordinary monsters' chances of gear, its
  quality and of junk, and the affix chance; ours are documented above (gear 12 % per roll, affix 60 %, junk 60 %).
  The world-loot buff scrolls (Scroll of Strength I–V...) do not exist here yet.
- Sparkles show where a monster left loot. Clicking walks up and opens a small loot window: the items with their
  descriptions; clicking an item takes it, "Take all" takes everything. An empty bag disappears. Stacks show "x10".

## Potions

- 5 tiers: Minor (level 1), Lesser (5), – (12), Greater (18), Major (24). They can only be drunk from that level;
  a potion on the action bar drinks that one, or the best of its kind once it runs out.
- A Potion heals over 20 s (every 2 s), a Serum (green) at once and more. All potions share a 60 s cooldown, as in the source game.
- Amounts are the source game's: Minor Life Potion 9 every 2 s (90), Major 400; Minor Life Serum 135, Major 600; mana 110 … 500, serums 162 … 720.
- Monsters drop potions by their level, 15 % a Serum instead of a Potion. Reginald transmutes 5 of a kind into the next tier
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
| Warrior | Mighty Blow, Holy Wrath, Radiance (1), Hammer of Might (2), Charge (3), Blessing of Champions (4), Divine Strike (5), Cleanse (6), Vengeance Aura (7), Blessing of Defense (8), Touch of Salvation (10) |
| Ranger | Sinister Strike, Entangling Shot, Mark Target (1), Multi-Shot (2), Stunning Shot (3), Focused Evasion (4), Blindside (5), Kidney Shot (6), Vanish (7), Sabotage (8), Aimed Shot (9), Devotion (10) |
| Mage | Fireball, Ignite, Chains of Ice (1), Scorch (2), Ice Shard (3), Boon of Clairvoyance (4), Ice Blast (5), Vim of Wisdom (6), Bind Spirit (7), Boon of Protection (8), Warmth (9), Teleport (10) |
| Warrior 11–20 | Salvation Aura (11), Retribution (12), Righteous Storm (13), Fortification Aura (14), Wings of Freedom (15), Aegis of Valor (16), Redemption (18), Shield Block (19), Divine Protection (20) |
| Ranger 11–20 | Sleep Arrow (11), Hamstring (12), Tactical Retreat (14), Ethereal Sting (15), Flame Arrow (16), Explosive Arrow (18), Eye Gouge (19), Arrow Flurry (20) |
| Mage 11–20 | Deep Freeze (11), Magical Amplification (12), Wisdom of Lazarus (14), Illusion Gate (15), Remove Curse (16), Magical Dampening (18), Antimagic (19), Inferno (20) |

- At the start 3 level-1 spells are known. The others are taught by Tomes (price 10 × level gold) from the trainers on the
  St. Revere square: Halfrid (Warrior), Kelda (Ranger), Gyldan (Mage) – the source game's trainers. A Tome can be read from the spell's level.
- **Ranks**: every spell has ranks 1–10, raised for XP in the spellbook (P). Price: 10 × spell level + 10 × rank^1.5
  (the formula is not in the source client – it is on its server). The rank raises damage/healing (in the formula) and effect strength.
- **Effects** (`StatusEffects`): stun, root, slow, damage and healing over time, more/less damage taken or dealt,
  attribute and Intelligence buffs, spell critical, dodge, "Evade", a shield (Absorb), "Incapacitate" (any damage breaks it),
  invisibility to monsters (Vanish), mana when it runs out (Devotion), stun immunity. Icons are under the player and target
  bars (green frame – buff, red – debuff).
- **Bar**: 12 slots like the source game's (keys 1–9, 0, -, =): Attack, spells and items, arranged by dragging
  (see [Crafting.md](Crafting.md)). A bar shows while casting; moving breaks the cast.
- Second-batch effects: Pacify (cannot attack), haste, invulnerability, mana burn, less healing, faster attacks,
  block rating, Silence (cannot cast). Redemption revives the nearest fallen friend on the spot
  (40 % HP, 20 % mana); Illusion Gate – takes you to St. Revere after 10 s; Tactical Retreat – a leap back from the enemy.
- Simplified: Charge – straight next to the target; Retribution always works (not only after a parry); Eye Gouge – less damage;
  Antimagic – Silence; Remove Curse – like Cleanse; Multi-Shot – 3 × 70 %; Ice Shard only works on slowed targets (Chains of Ice),
  Kidney Shot only on "Incapacitate"; Sabotage only on monsters not yet in combat; missing durations were chosen.

## Dungeons

All of the source game's dungeons that are entered from our part of the world (and from inside them), generated by
`Tools/GameData/dungeons.py` → `Scripts/Editor/DungeonData.cs` (the `.map` format is in `Tools/GameData/map_file.py`):

- **Plan** – from `maps/<map>.map` (cell flag 0 – walkable, 0x20 / 0x60 – wall): the rooms, corridors and
  pillars are the same. Arrival, exits, monsters (`npc`) and containers (`gameobject`) stand at their spots.
- **Entry rules** – `dungeon_template`: level, key (one is used up, only the opener's), number of players.
- Every group gets its own copy; monsters and chests do not come back; exits are always open.

| Dungeon (source map) | Entrance | Rules | Bosses | What the bosses drop |
|---|---|---|---|---|
| Wolf's Den (`wolf_cave_1`) | Wolf's Den | open | – (Cave Howler, Risen Miner, level 5) | – |
| Trog Cave (`goblin_cave`) | Trog Cave | open | – (Cave Trog, level 3) | – |
| The Lower Garrison (`stormrock_ruins`) | Atmar's Fall | level 25, up to 4 | Melchon (28) | **10 Tower Keys**, 3 Divine Accessory Orbs |
| Decrepit Tower (`fort_amir`) | Atmar's Fall | level 25, **Tower Key**, up to 4 | Lord Zargan (30), Tchazzar (30) | Divine orbs, 4 Magic Dust |
| Abandoned Barracks (`fort_nasu`) | Kandorys Castle | level 25, **Keys to the Barracks**, alone | Sarloch the Betrayer (25) | **Depleted Key** |
| Kandorys Undercourse (`dilapidated_sewers`) | Atmar's Fall | level 25, **Undercourse Key**, alone | Knight Maximus (28) | **2 Keys to St. Revere**, Divine Armor Orb |
| Desecrated Altar (`st_maria_1`) | by St. Revere | level 25, **Keys to St. Revere**, alone | Ebondeath (30) | **Salmaster's Key**, orbs, Magic Dust |
| Salmaster's Descent (`temple_of_mez_1`) | inside the Desecrated Altar | level 25, **Salmaster's Key**, alone | Salmaster (30), Balagos (30) | Major orbs, Divine Weapon Orb, gems |
| Telamont's Demiplane (`wizards_tower_2`) | Kandorys Forecourt | level 25, **Demiplane Key**, up to 4 | Demiplanar Guardian (30) | **20 Sanctum Keys**, Flawless and plain gems |
| Telamont's Sanctuary (`st_maria_3`) | inside the Demiplane | level 25, **Sanctum Key**, up to 4 | Prince Telamont (30) | 5 of each Flawless gem, Arcane Essence |


**The key chain:**

- Alone: Horned Charger / Horned Beast / Lesser Wyrm (5 %) → *Keys to the Barracks* → Sarloch → *Depleted Key*;
  + a *Dale Gem* (Zhendale camp, 15 %) – clicking combines them into an *Undercourse Key* → Knight Maximus → *Keys to St. Revere*
  → Ebondeath → *Salmaster's Key* → Salmaster's Descent.
- Group: Lower Garrison (no key) → Melchon → *Tower Key* → Decrepit Tower.
- Group: Tantorus / Fasthorn (100 %) → *Inactive Demiplane Key* + *Magic Dust* + *Major Treasury Gold* (jeweler) – clicking
  combines them into a *Demiplane Key* → Demiplanar Guardian → *Sanctum Key* → Telamont's Sanctuary.
- The Zhendale camp and Tantorus / Fasthorn stand where the source game has them (Hillsfar Dale, the west).

**Difficulty.** Levels are the source's (bosses 28–30; monster levels can go up to 30). The source's elites and "boss"-flagged
monsters are elites here. Where the source gives a monster's HP, it becomes a multiple of a normal monster of that level
(a normal level 25 monster there has 7340 HP): e.g. Melchon ≈ 16.5×, Lord Zargan ≈ 29×, Demiplanar Guardian ≈ 81×, Fallen
Guard ≈ 2.7×. Damage follows its `weapon_value` (400 – an ordinary elite): Kandorys Knight 7.5×, Guardian 11×.
Gold – `custom_gold_ratio`. Bosses have the ground slam (our mechanic).

**Chests** – the source game's containers at their spots, with their names and levels: Rosewood Crate (gold), Rosewood Chest
(locked, Lockpicking 50; a piece of gear, potion 75 %, gem 75 %), Platinum Strongbox (2 more pieces, potion and
gem 100 %, gold × 3), in the caves Elm / Aspen / Copper.

**Boss loot** – the source's `loot` tables (identical rows count once) and their quality chances (`loot_blue/gold/
purple_chance`, e.g. Lord Zargan – purple 100 %). Orbs, Magic Dust, Arcane Essence can only be sold for now (the source
price); the source's amethysts and stat scrolls are left out (we have none); Refined → our plain gem, Flawless → Flawless gem.
