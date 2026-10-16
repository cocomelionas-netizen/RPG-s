# Mana, loot quality and attributes

Inspired by the source game's mechanics.

## Mana

- A `Mana` component on the player: the server owns the value, it regenerates 3% per second, and is full after dying and respawning.
- Max mana = the class's `baseMana` + `manaPerLevel` × (level − 1) + Intelligence × 4 + items' `manaBonus`.
- Every ability has a `manaCost`; without mana – "Not enough mana" (`CastFailure.NoMana`).
  The action bar shows the cost under the name (red when there is not enough).
- Mana Potion (`ItemUse.RestoreMana`, 40%) – at vendors, in monster loot, 2 at the start.
- A blue mana bar above the experience bar.

## Junk and quality

- `ItemRarity`: Poor (grey), Common, Uncommon, Rare, Epic, Legendary.
- Monsters drop junk ~60% of the time (Goblin Ear, Brute Tusk, Orc Fang, Crown Shard), only good for selling.

## Random affixes

- `AffixDefinition` (a prefix "Sturdy Iron Helm" or a suffix "Iron Sword of the Bear") with attributes,
  damage, HP, armor, mana, quality, a level range and a price multiplier.
- `AffixCatalog` (`Data/Items/Affixes`): a 60% chance that a dropped weapon or armor piece gets an affix;
  rarer qualities roll less often (60/28/10/2), stronger affixes only from higher-level monsters.
- The affix is stored in `ItemStack.AffixId` (id = index + 1; only append to the list), saved with the account;
  items with different affixes do not stack. Quality = the better of the item's and the affix's.

## Attributes

- Strength, Agility, Intelligence, Vitality. A class has base attributes and a main attribute.
- Main attribute / 4 → damage; Vitality × 4 + Strength → HP; Intelligence × 4 → mana; Agility × 0.4% → critical chance.
- Every level gives 5 points. Window **C** (or the "Character (C)" button) – SPEND EXP, then "+" adds pending points and CONFIRM buys them (the server checks; a class trains only its own weapon skills).
- Items and affixes give attributes too. Old characters get points for the levels they already reached when they log in.
- The overall numbers at level 1 did not change (base HP and damage were lowered by what the attributes add).

## Tests

`Tests/PlayMode/AttributeAndManaTests.cs`, extended `AccountTests` and `ProgressionTests`.

## Character window (C) – in the source game's style

- "Name: value" rows (Health, Mana, Armor Value, Strength, Agility, Intelligence, Vitality, Damage,
  Critical Chance), colours taken from the source game's window: grey-sand labels, light values, mana blue.
- Next to Health, Mana and the four attributes – a small purple "+". Hovering: "+1 Strength: 1 point" and
  "You have invested N points." / "You haven't invested anything." (as in the source game). Hovering a row shows its description.
- Points are **bought with experience (XP)**, as in the source game: XP earned raises the level and also adds up in "Experience to spend";
  spending XP does not lower the level. There are no more points per level.
- Prices come from the source game's client (the client executable, function `0x558b50`), with `x` points already invested:
  - Strength, Agility, Intelligence, Vitality: `x^1.5 + 5` → 5, 6, 7, 10, 13, 16, 19, 23, 27, 32, 36…
  - Health, Mana (+1 per point): `x^(x/1500)`, at least 1 – 1 XP for a long time, only expensive with hundreds of points.
  - (The client also has: Regeneration/Meditate `x^1.4 + 4`, criticals `x/2 + 1` or `x/3 + 1`, Dodge/Block `x + 1`,
    resists `x/3 + 1`, Bartering/Lockpicking `2x + 1`, weapon skills `3 × the Strength price`; see [GameData.md](GameData.md).)
- The price colour is the same as the source game's (#B500AF); grey when there is not enough XP.
