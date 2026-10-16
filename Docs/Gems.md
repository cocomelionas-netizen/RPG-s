# Gems and sockets

| Gem | Attribute | Chipped | Plain | Flawless |
|---|---|---|---|---|
| Ruby | Strength | +2 | +4 | +7 |
| Topaz | Agility | +2 | +4 | +7 |
| Sapphire | Intelligence | +2 | +4 | +7 |
| Emerald | Vitality | +2 | +4 | +7 |

- **Sockets:** 25% of the weapons and armor monsters drop have a socket (`ItemStack.Socket`: 0 – none, -1 – empty,
  >0 – the item id of the gem set in it). The socket is shown in the description ("Socket: empty" / "Socket: Ruby (+4 Strength)").
- **Setting:** click a gem (its frame turns yellow), then an item – worn or in the bag. If it has an empty socket,
  the gem is set; if not, it says there is no room. Esc cancels. The gem stays for good.
- **The jeweler** Gemma Brightstone (in the south of the town) sells Chipped gems and with the
  "Combine gems" button turns every 3 of a kind into a better one (Chipped → plain → Flawless).
- **Drops:** Chipped – from all outdoor monsters (2–5%), plain – from orcs, Flawless – from bosses; dungeon bosses also drop plain and Flawless gems (see [GameData.md](GameData.md#dungeons)).
- An item with a gem sells for more (the gem's price is added). Sockets are saved with the account.

Tests: `Tests/PlayMode/GemTests.cs`.
