# Gems and sockets

The source game's gems (`item_template`): five kinds in three tiers; five of a kind transmute into the next tier at
Reginald (see [Crafting.md](Crafting.md)).

| Gem | Attribute | Rough (green) | Plain (blue) | Refined (gold) |
|---|---|---|---|---|
| Ruby | Strength | +2 | +4 | +6 |
| Emerald | Agility | +2 | +4 | +6 |
| Sapphire | Willpower | +2 | +4 | +6 |
| Amethyst | Intelligence | +2 | +4 | +6 |
| Topaz | Courage (health) | +2 | +4 | +6 |

- Values: the source game's 1 / 2 / 3, doubled for our attribute scale. Recipes: 5 Rough → plain; 5 plain + Treasury
  Coins → Refined. Vendors pay the source sell price (62 / 250 / 500).
- **Sockets:** up to 4, by quality like the source game's: plain none, green 0–1, blue 0–2, gold 1–3, purple 2–4
  (`ItemStack.Socket` … `Socket4`: 0 – none, -1 – empty, >0 – the item id of the gem set in it). Each socket is shown in the
  description ("Socket: empty" / "Socket: Ruby (+4 Strength)").
- **Setting:** click a gem (its frame turns yellow), then an item – worn or in the bag. If it has an empty socket,
  the gem goes into its first empty socket; if none is left, it says there is no room. Esc cancels. The gem stays for good.
- **Calder** ("Gems", St. Revere) sells the plain gems, three of each, as in the source game.
- **Drops:** Rough – from all outdoor monsters (2–5%) and chests, plain – from orcs, Refined – from bosses; dungeon bosses
  also drop plain and Refined gems (see [GameData.md](GameData.md#dungeons)).
- An item with a gem sells for more (the gem's price is added). Sockets are saved with the account.
- The asset files keep their old names (`ChippedRuby`, `FlawlessRuby`) so saved item ids stay.

Tests: `Tests/PlayMode/GemTests.cs`, `VendorTests.Transmuter_RoughGems_MakeAGem`.
