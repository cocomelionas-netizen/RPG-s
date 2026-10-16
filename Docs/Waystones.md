# Zones and waystones

## Zones

- A `Zone` (name + radius) marks places in the world; where zones overlap, the smaller one wins.
  Outside zones the map's name is used (`WorldNavigation.ZoneName`: "Greenvale Wilds", in a dungeon its name).
- Zones: **Greenvale** (town), **Brute Hills** (east), **Northern Ridge** (north), **Orc Marsh** (west), **Southern Fen** (south).
- Entering another zone shows a big name at the top of the screen (`ZoneBanner`) and the message "Entering ..."; the minimap shows the current zone.

(Since then the zones and waystones are the source game's named places – see [GameData.md](GameData.md#map).)

## Waystones

- A stone with glowing runes stands in every zone (a light blue dot on the minimap).
- Coming within 3 units **discovers** the stone ("You've discovered a new waypoint! ...").
- Clicking the stone opens a list: discovered stones can be teleported to **for free** (as the source game's waypoints), undiscovered ones are grey.
  `WaystoneTraveller.travelCost` can still set a price (0 by default).
- The server checks: the player stands at the stone, the destination is discovered, and the price if there is one (`WaystoneTraveller.ServerTravel`).
- Discovered stones are saved with the account (`CharacterRecord.waystones`, bits by the stone's `Id`; never change ids).

Tests: `Tests/PlayMode/WaystoneTests.cs`, extended `AccountTests`.
