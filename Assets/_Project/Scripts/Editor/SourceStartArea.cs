// Generated from Source's game.db (map "fanadin", start region x 0-250, y 30-260): where monsters and
// chests stand and how fast they come back. Cells are already in this project's map coordinates.
using UnityEngine;

namespace RPG.EditorTools
{
    internal static class SourceStartArea
    {
        public const int Width = 256;
        public const int Height = 236;
        /// <summary>The player start (Source's "start" teleport, 17, 106): the town is built around it.</summary>
        public static readonly Vector3Int Town = new Vector3Int(19, 78, 0);
        /// <summary>Wolf's Den: the dungeon entrance.</summary>
        public static readonly Vector3Int DungeonEntrance = new Vector3Int(79, 139, 0);

        /// <summary>Monster spots: cell, Source level, elite, respawn seconds.</summary>
        public static readonly (Vector3Int cell, int level, bool elite, float respawn)[] Monsters =
        {
            (new Vector3Int(67, 40, 0), 1, false, 5f), // Antling
            (new Vector3Int(68, 47, 0), 1, false, 5f), // Antling
            (new Vector3Int(76, 42, 0), 1, false, 5f), // Antling
            (new Vector3Int(80, 37, 0), 1, false, 5f), // Antling
            (new Vector3Int(81, 63, 0), 1, false, 5f), // Antling
            (new Vector3Int(81, 50, 0), 1, false, 5f), // Antling
            (new Vector3Int(89, 43, 0), 1, false, 5f), // Antling
            (new Vector3Int(91, 145, 0), 5, true, 90f), // Drauglyen
            (new Vector3Int(96, 126, 0), 4, false, 60f), // Howler
            (new Vector3Int(98, 44, 0), 1, false, 5f), // Antling
            (new Vector3Int(98, 32, 0), 3, true, 90f), // Antling Broodmother
            (new Vector3Int(101, 56, 0), 1, false, 5f), // Antling
            (new Vector3Int(103, 110, 0), 4, false, 60f), // Howler
            (new Vector3Int(107, 137, 0), 4, false, 60f), // Howler
            (new Vector3Int(109, 86, 0), 4, false, 60f), // Howler
            (new Vector3Int(112, 45, 0), 1, false, 5f), // Antling
            (new Vector3Int(113, 61, 0), 1, false, 5f), // Antling
            (new Vector3Int(114, 27, 0), 1, false, 5f), // Antling
            (new Vector3Int(114, 119, 0), 4, false, 60f), // Howler
            (new Vector3Int(122, 29, 0), 1, false, 5f), // Antling
            (new Vector3Int(123, 42, 0), 1, false, 5f), // Antling
            (new Vector3Int(130, 68, 0), 1, false, 5f), // Antling
            (new Vector3Int(132, 126, 0), 4, false, 60f), // Howler
            (new Vector3Int(134, 33, 0), 2, false, 5f), // Antwig
            (new Vector3Int(139, 73, 0), 1, false, 5f), // Antling
            (new Vector3Int(142, 36, 0), 2, false, 5f), // Antwig
            (new Vector3Int(143, 62, 0), 1, false, 5f), // Antling
            (new Vector3Int(148, 49, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(151, 40, 0), 1, false, 5f), // Antling
            (new Vector3Int(151, 115, 0), 4, false, 60f), // Howler
            (new Vector3Int(157, 87, 0), 1, false, 5f), // Antling
            (new Vector3Int(157, 95, 0), 1, false, 5f), // Antling
            (new Vector3Int(158, 138, 0), 4, false, 60f), // Howler
            (new Vector3Int(162, 121, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(163, 83, 0), 2, false, 5f), // Antwig
            (new Vector3Int(163, 46, 0), 2, false, 5f), // Antwig
            (new Vector3Int(166, 62, 0), 2, false, 5f), // Antwig
            (new Vector3Int(167, 103, 0), 2, false, 5f), // Antwig
            (new Vector3Int(172, 120, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(172, 84, 0), 1, false, 5f), // Antling
            (new Vector3Int(172, 144, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(173, 40, 0), 2, false, 5f), // Antwig
            (new Vector3Int(175, 54, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(176, 36, 0), 1, false, 5f), // Antling
            (new Vector3Int(176, 96, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(178, 112, 0), 1, false, 5f), // Antling
            (new Vector3Int(179, 131, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(180, 66, 0), 1, false, 5f), // Antling
            (new Vector3Int(180, 166, 0), 5, false, 60f), // Cokatrice
            (new Vector3Int(184, 138, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(187, 147, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(187, 185, 0), 1, false, 300f), // Antling
            (new Vector3Int(188, 138, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(189, 33, 0), 2, false, 5f), // Antwig
            (new Vector3Int(189, 57, 0), 2, false, 5f), // Antwig
            (new Vector3Int(189, 158, 0), 1, false, 300f), // Antling
            (new Vector3Int(191, 191, 0), 1, false, 300f), // Antling
            (new Vector3Int(192, 46, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(193, 128, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(194, 154, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(195, 170, 0), 1, false, 300f), // Antling
            (new Vector3Int(196, 145, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(201, 29, 0), 3, false, 60f), // Trog
            (new Vector3Int(202, 165, 0), 2, false, 5f), // Antwig
            (new Vector3Int(204, 140, 0), 5, false, 60f), // Cokatrice
            (new Vector3Int(205, 94, 0), 2, false, 5f), // Antwig
            (new Vector3Int(206, 82, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(209, 28, 0), 3, false, 60f), // Trog
            (new Vector3Int(209, 56, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(210, 35, 0), 3, false, 60f), // Trog
            (new Vector3Int(211, 159, 0), 1, false, 300f), // Antling
            (new Vector3Int(212, 133, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(215, 182, 0), 5, false, 60f), // Terror Clan Warrior
            (new Vector3Int(216, 165, 0), 5, false, 60f), // Terror Clan Warrior
            (new Vector3Int(217, 96, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(219, 65, 0), 2, false, 5f), // Antwig
            (new Vector3Int(219, 154, 0), 5, false, 60f), // Terror Clan Archer
            (new Vector3Int(221, 51, 0), 2, false, 5f), // Antwig
            (new Vector3Int(223, 176, 0), 5, false, 60f), // Terror Clan Archer
            (new Vector3Int(227, 31, 0), 2, false, 5f), // Antwig
            (new Vector3Int(228, 157, 0), 5, false, 60f), // Terror Clan Warrior
            (new Vector3Int(234, 44, 0), 2, false, 5f), // Antwig
            (new Vector3Int(239, 72, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(244, 80, 0), 1, false, 5f), // Antling
            (new Vector3Int(245, 46, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(247, 33, 0), 3, false, 60f), // Trog
            (new Vector3Int(251, 95, 0), 2, false, 5f), // Petram Cokatrice
        };

        /// <summary>Containers: cell, kind (0 crate or barrel, 1 chest), level, respawn seconds.</summary>
        public static readonly (Vector3Int cell, int kind, int level, float respawn)[] Chests =
        {
            (new Vector3Int(169, 48, 0), 0, 3, 60f), // Aspen Crate Lv3
            (new Vector3Int(121, 49, 0), 0, 3, 60f), // Aspen Barrel Lv3
            (new Vector3Int(75, 30, 0), 1, 3, 300f), // Aspen Chest
            (new Vector3Int(99, 27, 0), 1, 4, 300f), // Aspen Chest Lv4
            (new Vector3Int(206, 27, 0), 0, 3, 60f), // Aspen Barrel Lv3
            (new Vector3Int(208, 25, 0), 0, 3, 60f), // Aspen Crate Lv3
            (new Vector3Int(170, 37, 0), 1, 3, 300f), // Aspen Chest
            (new Vector3Int(142, 27, 0), 1, 3, 300f), // Aspen Chest
            (new Vector3Int(110, 19, 0), 1, 4, 300f), // Aspen Chest Lv4
            (new Vector3Int(237, 53, 0), 1, 3, 300f), // Aspen Chest
            (new Vector3Int(185, 143, 0), 0, 6, 60f), // Elm Barrel Lv6
            (new Vector3Int(200, 98, 0), 0, 6, 60f), // Elm Barrel Lv6
            (new Vector3Int(185, 142, 0), 1, 6, 300f), // Elm Chest Lv6
            (new Vector3Int(182, 181, 0), 1, 7, 300f), // Elm Chest Lv7
            (new Vector3Int(189, 165, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(122, 112, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(84, 99, 0), 1, 7, 300f), // Elm Chest Lv7
            (new Vector3Int(85, 99, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(90, 146, 0), 1, 7, 300f), // Elm Chest Lv7
            (new Vector3Int(92, 135, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(90, 119, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(192, 216, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(137, 221, 0), 0, 7, 60f), // Elm Barrel Lv7
            (new Vector3Int(136, 221, 0), 0, 7, 60f), // Elm Barrel Lv7
            (new Vector3Int(137, 222, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(58, 212, 0), 0, 7, 60f), // Elm Barrel Lv7
            (new Vector3Int(69, 211, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(91, 192, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(147, 201, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(230, 60, 0), 0, 3, 60f), // Aspen Crate Lv3
            (new Vector3Int(245, 126, 0), 1, 12, 300f), // Cherry Chest Lv12
            (new Vector3Int(241, 29, 0), 0, 3, 60f), // Aspen Barrel Lv3
            (new Vector3Int(245, 175, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(241, 220, 0), 1, 25, 300f), // Rosewood Chest Lv25
        };

        /// <summary>Waystones (Source's waypoints and the start) with the zone around each: name, stone cell, zone radius in cells.</summary>
        public static readonly (string name, Vector3Int cell, float zoneRadius)[] Places =
        {
            ("Pilgrim's Refuge", new Vector3Int(19, 78, 0), 12f),
            ("Merchant's Way", new Vector3Int(62, 38, 0), 22f),
            ("Fields of Dreams", new Vector3Int(185, 61, 0), 32f),
            ("Howler's Valley", new Vector3Int(103, 123, 0), 26f),
            ("Thicket of the Wanderer", new Vector3Int(193, 200, 0), 28f),
        };

        /// <summary>Named places without a waystone.</summary>
        public static readonly (string name, Vector3Int cell, float zoneRadius)[] OtherZones =
        {
            ("Wolf's Den", new Vector3Int(79, 139, 0), 8f),
            ("Trog Cave", new Vector3Int(241, 26, 0), 10f),
        };
    }
}
