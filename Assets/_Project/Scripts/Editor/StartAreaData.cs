// Generated from the source game's game.db (map "fanadin", x 0-560, y 30-340: the start, Pilgrim's Refuge, up to the
// main town, St. Revere): where monsters and chests stand and how fast they come back, in this project's cells.
using UnityEngine;

namespace RPG.EditorTools
{
    internal static class StartAreaData
    {
        public const int Width = 568;
        public const int Height = 318;
        /// <summary>Where new characters start: the source game's "start" teleport in Pilgrim's Refuge (17, 106).</summary>
        public static readonly Vector3Int Start = new Vector3Int(19, 78, 0);
        /// <summary>The main town, St. Revere: the middle of its market (524, 250).</summary>
        public static readonly Vector3Int Town = new Vector3Int(526, 222, 0);
        /// <summary>Vendors where the source game has them in Revere: heavy armor (warrior), ranged (ranger), staves (mage), jeweler.</summary>
        public static readonly Vector3Int[] Vendors =
        {
            new Vector3Int(523, 220, 0), // Karl
            new Vector3Int(531, 207, 0), // Selena
            new Vector3Int(522, 207, 0), // Dragos
            new Vector3Int(523, 216, 0), // Vikram
        };
        /// <summary>Wolf's Den: the dungeon entrance.</summary>
        public static readonly Vector3Int DungeonEntrance = new Vector3Int(79, 139, 0);

        /// <summary>Monster spots: cell, source-game level, elite (or boss), respawn seconds (capped at 10 minutes).</summary>
        public static readonly (Vector3Int cell, int level, bool elite, float respawn)[] Monsters =
        {
            (new Vector3Int(51, 218, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(54, 236, 0), 12, true, 90f), // Loglan The Overseer
            (new Vector3Int(57, 306, 0), 11, true, 90f), // Dabbuz The Bushwhacker
            (new Vector3Int(57, 242, 0), 11, false, 60f), // Blightchosen Acolyte
            (new Vector3Int(61, 300, 0), 11, false, 60f), // Blightchosen Acolyte
            (new Vector3Int(62, 227, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(64, 216, 0), 11, false, 60f), // Blightchosen Acolyte
            (new Vector3Int(67, 40, 0), 1, false, 5f), // Antling
            (new Vector3Int(68, 47, 0), 1, false, 5f), // Antling
            (new Vector3Int(69, 248, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(74, 282, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(75, 217, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(76, 42, 0), 1, false, 5f), // Antling
            (new Vector3Int(80, 37, 0), 1, false, 5f), // Antling
            (new Vector3Int(81, 63, 0), 1, false, 5f), // Antling
            (new Vector3Int(81, 50, 0), 1, false, 5f), // Antling
            (new Vector3Int(83, 265, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(89, 43, 0), 1, false, 5f), // Antling
            (new Vector3Int(91, 145, 0), 5, true, 90f), // Drauglyen
            (new Vector3Int(96, 126, 0), 4, false, 60f), // Howler
            (new Vector3Int(98, 44, 0), 1, false, 5f), // Antling
            (new Vector3Int(98, 32, 0), 3, true, 90f), // Antling Broodmother
            (new Vector3Int(100, 192, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(101, 56, 0), 1, false, 5f), // Antling
            (new Vector3Int(103, 110, 0), 4, false, 60f), // Howler
            (new Vector3Int(107, 137, 0), 4, false, 60f), // Howler
            (new Vector3Int(109, 86, 0), 4, false, 60f), // Howler
            (new Vector3Int(112, 45, 0), 1, false, 5f), // Antling
            (new Vector3Int(113, 61, 0), 1, false, 5f), // Antling
            (new Vector3Int(114, 27, 0), 1, false, 5f), // Antling
            (new Vector3Int(114, 119, 0), 4, false, 60f), // Howler
            (new Vector3Int(118, 299, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(122, 29, 0), 1, false, 5f), // Antling
            (new Vector3Int(123, 42, 0), 1, false, 5f), // Antling
            (new Vector3Int(128, 227, 0), 11, false, 60f), // Blightchosen Acolyte
            (new Vector3Int(130, 68, 0), 1, false, 5f), // Antling
            (new Vector3Int(131, 241, 0), 11, true, 90f), // Shav The Quick
            (new Vector3Int(132, 126, 0), 4, false, 60f), // Howler
            (new Vector3Int(134, 33, 0), 2, false, 5f), // Antwig
            (new Vector3Int(139, 73, 0), 1, false, 5f), // Antling
            (new Vector3Int(142, 36, 0), 2, false, 5f), // Antwig
            (new Vector3Int(142, 200, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(143, 62, 0), 1, false, 5f), // Antling
            (new Vector3Int(144, 217, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(144, 243, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(148, 49, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(151, 40, 0), 1, false, 5f), // Antling
            (new Vector3Int(151, 115, 0), 4, false, 60f), // Howler
            (new Vector3Int(157, 87, 0), 1, false, 5f), // Antling
            (new Vector3Int(157, 95, 0), 1, false, 5f), // Antling
            (new Vector3Int(157, 264, 0), 11, false, 60f), // Blightchosen Acolyte
            (new Vector3Int(158, 138, 0), 4, false, 60f), // Howler
            (new Vector3Int(158, 286, 0), 11, false, 60f), // Blightchosen Guardsman
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
            (new Vector3Int(181, 219, 0), 11, false, 60f), // Blightchosen Guardsman
            (new Vector3Int(184, 138, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(187, 147, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(187, 185, 0), 1, false, 300f), // Antling
            (new Vector3Int(188, 138, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(189, 33, 0), 2, false, 5f), // Antwig
            (new Vector3Int(189, 57, 0), 2, false, 5f), // Antwig
            (new Vector3Int(189, 158, 0), 1, false, 300f), // Antling
            (new Vector3Int(191, 191, 0), 1, false, 300f), // Antling
            (new Vector3Int(192, 186, 0), 12, false, 60f), // Ice Antwig
            (new Vector3Int(192, 46, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(193, 128, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(194, 154, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(195, 170, 0), 1, false, 300f), // Antling
            (new Vector3Int(196, 145, 0), 4, false, 60f), // Trog Brute
            (new Vector3Int(201, 29, 0), 3, false, 60f), // Trog
            (new Vector3Int(201, 180, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(202, 165, 0), 2, false, 5f), // Antwig
            (new Vector3Int(203, 210, 0), 12, false, 60f), // Fire Cokatrice
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
            (new Vector3Int(217, 281, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(217, 96, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(219, 65, 0), 2, false, 5f), // Antwig
            (new Vector3Int(219, 154, 0), 5, false, 60f), // Terror Clan Archer
            (new Vector3Int(221, 51, 0), 2, false, 5f), // Antwig
            (new Vector3Int(223, 176, 0), 5, false, 60f), // Terror Clan Archer
            (new Vector3Int(225, 236, 0), 12, false, 60f), // Fire Antwig
            (new Vector3Int(227, 31, 0), 2, false, 5f), // Antwig
            (new Vector3Int(228, 157, 0), 5, false, 60f), // Terror Clan Warrior
            (new Vector3Int(229, 241, 0), 1, false, 300f), // Antling
            (new Vector3Int(229, 236, 0), 1, false, 300f), // Antling
            (new Vector3Int(234, 44, 0), 2, false, 5f), // Antwig
            (new Vector3Int(239, 72, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(242, 143, 0), 20, false, 300f), // Horned Beast
            (new Vector3Int(244, 80, 0), 1, false, 5f), // Antling
            (new Vector3Int(244, 220, 0), 25, true, 30f), // Lesser Wyrm
            (new Vector3Int(245, 46, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(247, 33, 0), 3, false, 60f), // Trog
            (new Vector3Int(250, 272, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(251, 95, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(252, 296, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(254, 205, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(254, 156, 0), 20, false, 300f), // Horned Beast
            (new Vector3Int(255, 172, 0), 20, false, 300f), // Horned Charger
            (new Vector3Int(257, 222, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(258, 239, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(259, 258, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(260, 49, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(261, 130, 0), 20, false, 300f), // Horned Charger
            (new Vector3Int(261, 82, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(262, 270, 0), 25, true, 30f), // Lesser Wyrm
            (new Vector3Int(265, 179, 0), 20, false, 300f), // Horned Beast
            (new Vector3Int(266, 63, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(267, 284, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(267, 162, 0), 20, false, 300f), // Horned Charger
            (new Vector3Int(267, 36, 0), 3, false, 60f), // Trog
            (new Vector3Int(267, 307, 0), 25, true, 30f), // Lesser Wyrm
            (new Vector3Int(269, 101, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(270, 29, 0), 3, false, 60f), // Trog
            (new Vector3Int(275, 79, 0), 6, false, 60f), // Lone Howler
            (new Vector3Int(276, 31, 0), 3, false, 60f), // Trog
            (new Vector3Int(277, 41, 0), 3, false, 60f), // Trog
            (new Vector3Int(277, 224, 0), 25, true, 30f), // Lesser Wyrm
            (new Vector3Int(280, 243, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(287, 59, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(288, 260, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(290, 130, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(290, 31, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(291, 272, 0), 20, false, 300f), // Horned Charger
            (new Vector3Int(293, 87, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(296, 118, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(297, 294, 0), 25, true, 30f), // Lesser Wyrm
            (new Vector3Int(298, 307, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(298, 73, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(299, 231, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(300, 281, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(300, 32, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(301, 43, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(302, 89, 0), 1, false, 300f), // Antling
            (new Vector3Int(303, 182, 0), 17, false, 90f), // Yargol Fighter
            (new Vector3Int(304, 218, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(304, 61, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(304, 169, 0), 16, false, 90f), // Yargol Grunt
            (new Vector3Int(305, 157, 0), 17, false, 90f), // Yargol Fighter
            (new Vector3Int(306, 247, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(307, 298, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(309, 163, 0), 18, true, 90f), // Morgall
            (new Vector3Int(310, 178, 0), 16, false, 90f), // Yargol Grunt
            (new Vector3Int(312, 52, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(312, 189, 0), 16, false, 90f), // Yargol Grunt
            (new Vector3Int(312, 262, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(313, 87, 0), 12, false, 60f), // Fire Antwig
            (new Vector3Int(315, 164, 0), 17, false, 90f), // Yargol Fighter
            (new Vector3Int(318, 216, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(320, 58, 0), 2, false, 5f), // Antwig
            (new Vector3Int(321, 306, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(321, 254, 0), 25, true, 30f), // Lesser Wyrm
            (new Vector3Int(323, 191, 0), 17, false, 90f), // Yargol Fighter
            (new Vector3Int(325, 185, 0), 16, false, 90f), // Yargol Grunt
            (new Vector3Int(325, 62, 0), 1, false, 300f), // Antling
            (new Vector3Int(326, 233, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(326, 40, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(329, 215, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(332, 89, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(333, 137, 0), 2, false, 5f), // Petram Cokatrice
            (new Vector3Int(334, 77, 0), 12, false, 60f), // Fire Antwig
            (new Vector3Int(336, 108, 0), 7, false, 60f), // Yargol
            (new Vector3Int(336, 27, 0), 14, true, 90f), // Ghastly Arachnid
            (new Vector3Int(337, 182, 0), 17, false, 90f), // Yargol Fighter
            (new Vector3Int(341, 216, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(341, 46, 0), 3, false, 5f), // Spiderling
            (new Vector3Int(342, 79, 0), 1, false, 300f), // Antling
            (new Vector3Int(342, 72, 0), 1, false, 300f), // Antling
            (new Vector3Int(343, 99, 0), 7, false, 60f), // Yargol
            (new Vector3Int(345, 245, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(345, 75, 0), 12, false, 60f), // Ice Antwig
            (new Vector3Int(349, 113, 0), 7, false, 60f), // Yargol
            (new Vector3Int(353, 49, 0), 1, false, 300f), // Antling
            (new Vector3Int(354, 116, 0), 7, false, 60f), // Yargol
            (new Vector3Int(354, 97, 0), 9, true, 90f), // Argha The Plunderer
            (new Vector3Int(355, 54, 0), 12, false, 60f), // Fire Antwig
            (new Vector3Int(355, 308, 0), 25, true, 30f), // Lesser Wyrm
            (new Vector3Int(357, 293, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(357, 37, 0), 12, false, 60f), // Azure Roc
            (new Vector3Int(358, 252, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(358, 70, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(359, 173, 0), 1, false, 300f), // Antling
            (new Vector3Int(361, 57, 0), 1, false, 300f), // Antling
            (new Vector3Int(364, 278, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(366, 89, 0), 1, false, 300f), // Antling
            (new Vector3Int(367, 60, 0), 12, false, 60f), // Ice Antwig
            (new Vector3Int(367, 175, 0), 2, false, 5f), // Antwig
            (new Vector3Int(368, 85, 0), 12, false, 60f), // Fire Antwig
            (new Vector3Int(368, 55, 0), 1, false, 300f), // Antling
            (new Vector3Int(368, 171, 0), 1, false, 300f), // Antling
            (new Vector3Int(370, 48, 0), 12, false, 60f), // Fire Antwig
            (new Vector3Int(372, 220, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(374, 249, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(375, 74, 0), 12, false, 60f), // Azure Roc
            (new Vector3Int(379, 46, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(384, 195, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(385, 290, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(385, 101, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(386, 68, 0), 12, false, 60f), // Ice Antwig
            (new Vector3Int(388, 59, 0), 1, false, 300f), // Antling
            (new Vector3Int(391, 72, 0), 1, false, 300f), // Antling
            (new Vector3Int(393, 282, 0), 20, false, 30f), // Horned Charger
            (new Vector3Int(393, 39, 0), 12, false, 60f), // Fire Antwig
            (new Vector3Int(393, 57, 0), 12, false, 60f), // Ice Antwig
            (new Vector3Int(396, 294, 0), 25, true, 30f), // Lesser Wyrm
            (new Vector3Int(398, 74, 0), 12, false, 60f), // Fire Antwig
            (new Vector3Int(399, 68, 0), 1, false, 300f), // Antling
            (new Vector3Int(399, 87, 0), 12, false, 60f), // Azure Roc
            (new Vector3Int(402, 242, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(408, 38, 0), 5, false, 60f), // Cokatrice
            (new Vector3Int(409, 75, 0), 12, false, 60f), // Ice Antwig
            (new Vector3Int(411, 277, 0), 20, false, 30f), // Horned Beast
            (new Vector3Int(413, 109, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(415, 79, 0), 1, false, 300f), // Antling
            (new Vector3Int(416, 89, 0), 1, false, 300f), // Antling
            (new Vector3Int(420, 83, 0), 2, false, 5f), // Antwig
            (new Vector3Int(421, 32, 0), 5, false, 60f), // Trog Shaman
            (new Vector3Int(422, 78, 0), 1, false, 300f), // Antling
            (new Vector3Int(424, 43, 0), 5, false, 60f), // Trog Raider
            (new Vector3Int(427, 32, 0), 5, false, 60f), // Trog Raider
            (new Vector3Int(430, 78, 0), 12, false, 60f), // Ice Antwig
            (new Vector3Int(430, 39, 0), 5, false, 60f), // Trog Raider
            (new Vector3Int(436, 49, 0), 12, false, 60f), // Fire Antwig
            (new Vector3Int(438, 307, 0), 5, false, 60f), // Quazit
            (new Vector3Int(443, 280, 0), 5, false, 60f), // Tasloi Scout
            (new Vector3Int(446, 250, 0), 5, false, 60f), // Quazit
            (new Vector3Int(450, 69, 0), 15, false, 90f), // Watcher of Atmar
            (new Vector3Int(450, 287, 0), 5, false, 60f), // Tasloi Scout
            (new Vector3Int(452, 35, 0), 15, false, 90f), // Seer of Atmar
            (new Vector3Int(454, 55, 0), 15, false, 90f), // Watcher of Atmar
            (new Vector3Int(455, 88, 0), 15, false, 90f), // Seer of Atmar
            (new Vector3Int(456, 270, 0), 5, false, 60f), // Antipede
            (new Vector3Int(456, 309, 0), 5, false, 60f), // Antipede
            (new Vector3Int(462, 111, 0), 12, false, 60f), // Fire Cokatrice
            (new Vector3Int(470, 89, 0), 15, false, 90f), // Seer of Atmar
            (new Vector3Int(470, 38, 0), 15, false, 90f), // Watcher of Atmar
            (new Vector3Int(471, 50, 0), 15, false, 90f), // Seer of Atmar
            (new Vector3Int(471, 71, 0), 15, false, 90f), // Watcher of Atmar
            (new Vector3Int(475, 273, 0), 5, false, 60f), // Antipede
            (new Vector3Int(476, 308, 0), 7, true, 90f), // Shriekevil The Drooling
            (new Vector3Int(487, 62, 0), 15, false, 90f), // Seer of Atmar
            (new Vector3Int(500, 260, 0), 1, false, 300f), // Antling
            (new Vector3Int(501, 48, 0), 15, false, 90f), // Watcher of Atmar
            (new Vector3Int(504, 151, 0), 1, false, 300f), // Antling
            (new Vector3Int(507, 115, 0), 1, false, 300f), // Antling
            (new Vector3Int(511, 33, 0), 15, false, 90f), // Watcher of Atmar
            (new Vector3Int(516, 226, 0), 1, false, 300f), // Antling
            (new Vector3Int(516, 202, 0), 1, false, 300f), // Antling
            (new Vector3Int(518, 48, 0), 15, false, 90f), // Seer of Atmar
            (new Vector3Int(521, 65, 0), 15, false, 90f), // Watcher of Atmar
            (new Vector3Int(528, 28, 0), 15, false, 90f), // Seer of Atmar
            (new Vector3Int(536, 46, 0), 15, false, 90f), // Watcher of Atmar
            (new Vector3Int(542, 304, 0), 1, false, 300f), // Antling
            (new Vector3Int(550, 198, 0), 1, false, 300f), // Antling
        };

        /// <summary>Containers: cell, kind (0 crate or barrel, 1 chest), level, respawn seconds (capped at 10 minutes).</summary>
        public static readonly (Vector3Int cell, int kind, int level, float respawn)[] Chests =
        {
            (new Vector3Int(169, 48, 0), 0, 3, 60f), // Aspen Crate Lv3
            (new Vector3Int(121, 49, 0), 0, 3, 60f), // Aspen Barrel Lv3
            (new Vector3Int(75, 30, 0), 1, 3, 300f), // Aspen Chest
            (new Vector3Int(99, 27, 0), 1, 4, 300f), // Aspen Chest Lv4
            (new Vector3Int(206, 27, 0), 0, 3, 60f), // Aspen Barrel Lv3
            (new Vector3Int(208, 25, 0), 0, 3, 60f), // Aspen Crate Lv3
            (new Vector3Int(267, 29, 0), 0, 3, 60f), // Aspen Barrel Lv3
            (new Vector3Int(273, 28, 0), 0, 3, 60f), // Aspen Crate Lv3
            (new Vector3Int(170, 37, 0), 1, 3, 300f), // Aspen Chest
            (new Vector3Int(142, 27, 0), 1, 3, 300f), // Aspen Chest
            (new Vector3Int(110, 19, 0), 1, 4, 300f), // Aspen Chest Lv4
            (new Vector3Int(237, 53, 0), 1, 3, 300f), // Aspen Chest
            (new Vector3Int(185, 143, 0), 0, 6, 60f), // Elm Barrel Lv6
            (new Vector3Int(404, 25, 0), 1, 7, 300f), // Elm Chest Lv7
            (new Vector3Int(420, 31, 0), 1, 6, 300f), // Elm Chest Lv6
            (new Vector3Int(424, 28, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(337, 23, 0), 1, 9, 600f), // Bronze Strongbox lv9
            (new Vector3Int(308, 89, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(368, 26, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(362, 88, 0), 0, 6, 60f), // Elm Crate Lv6
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
            (new Vector3Int(139, 238, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(132, 242, 0), 1, 7, 300f), // Elm Chest Lv7
            (new Vector3Int(158, 282, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(53, 307, 0), 0, 7, 60f), // Elm Barrel Lv7
            (new Vector3Int(56, 304, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(55, 301, 0), 1, 7, 300f), // Elm Chest Lv7
            (new Vector3Int(65, 291, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(54, 244, 0), 0, 7, 60f), // Elm Barrel Lv7
            (new Vector3Int(53, 245, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(58, 212, 0), 0, 7, 60f), // Elm Barrel Lv7
            (new Vector3Int(69, 211, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(91, 192, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(147, 201, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(53, 234, 0), 1, 7, 300f), // Elm Chest Lv7
            (new Vector3Int(230, 60, 0), 0, 3, 60f), // Aspen Crate Lv3
            (new Vector3Int(267, 124, 0), 0, 12, 60f), // Cherry Crate Lv12
            (new Vector3Int(245, 126, 0), 1, 12, 300f), // Cherry Chest Lv12
            (new Vector3Int(247, 238, 0), 0, 12, 60f), // Cherry Barrel Lv12
            (new Vector3Int(316, 161, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(301, 178, 0), 0, 7, 60f), // Elm Barrel Lv7
            (new Vector3Int(348, 92, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(355, 117, 0), 0, 6, 60f), // Elm Crate Lv6
            (new Vector3Int(355, 118, 0), 0, 6, 60f), // Elm Barrel Lv6
            (new Vector3Int(342, 310, 0), 0, 13, 60f), // Cherry Crate Lv13
            (new Vector3Int(355, 95, 0), 1, 12, 300f), // Cherry Chest Lv12
            (new Vector3Int(253, 44, 0), 0, 3, 60f), // Aspen Crate Lv3
            (new Vector3Int(241, 29, 0), 0, 3, 60f), // Aspen Barrel Lv3
            (new Vector3Int(442, 282, 0), 0, 4, 60f), // Aspen Barrel Lv4
            (new Vector3Int(448, 282, 0), 0, 7, 60f), // Elm Crate Lv7
            (new Vector3Int(52, 236, 0), 1, 12, 600f), // Silver Strongbox Lv12
            (new Vector3Int(348, 64, 0), 0, 13, 60f), // Cherry Crate Lv13
            (new Vector3Int(342, 27, 0), 0, 12, 60f), // Cherry Barrel Lv12
            (new Vector3Int(363, 52, 0), 1, 12, 300f), // Cherry Chest Lv12
            (new Vector3Int(378, 92, 0), 0, 13, 60f), // Cherry Crate Lv13
            (new Vector3Int(392, 64, 0), 1, 12, 300f), // Cherry Chest Lv12
            (new Vector3Int(448, 80, 0), 0, 14, 60f), // Birch Crate Lv14
            (new Vector3Int(463, 50, 0), 0, 14, 60f), // Birch Crate Lv14
            (new Vector3Int(497, 56, 0), 0, 14, 60f), // Birch Crate Lv14
            (new Vector3Int(515, 59, 0), 0, 14, 60f), // Birch Crate Lv14
            (new Vector3Int(537, 66, 0), 0, 14, 60f), // Birch Crate Lv14
            (new Vector3Int(543, 39, 0), 0, 14, 60f), // Birch Crate Lv14
            (new Vector3Int(526, 44, 0), 0, 13, 60f), // Cherry Barrel Lv13
            (new Vector3Int(495, 44, 0), 0, 13, 60f), // Cherry Barrel Lv13
            (new Vector3Int(481, 62, 0), 0, 13, 60f), // Cherry Barrel Lv13
            (new Vector3Int(495, 29, 0), 1, 14, 300f), // Birch Chest Lv14
            (new Vector3Int(382, 292, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(414, 272, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(399, 308, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(286, 163, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(266, 172, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(245, 175, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(302, 153, 0), 1, 18, 300f), // Spruce Chest Lv18
            (new Vector3Int(247, 247, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(322, 264, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(289, 285, 0), 0, 20, 300f), // Mahogany Crate Lv20
            (new Vector3Int(249, 280, 0), 0, 24, 30f), // Ebony Crate Lv24
            (new Vector3Int(321, 203, 0), 0, 24, 30f), // Ebony Crate Lv24
            (new Vector3Int(289, 216, 0), 0, 24, 30f), // Ebony Crate Lv24
            (new Vector3Int(265, 254, 0), 0, 24, 300f), // Ebony Crate Lv24
            (new Vector3Int(320, 250, 0), 1, 24, 300f), // Ebony Chest Lv24
            (new Vector3Int(284, 264, 0), 0, 25, 30f), // Rosewood Crate Lv25
            (new Vector3Int(241, 220, 0), 1, 25, 300f), // Rosewood Chest Lv25
            (new Vector3Int(290, 270, 0), 1, 25, 300f), // Rosewood Chest Lv25
        };

        /// <summary>
        /// Waystones where the source game has waypoints and teleports, with the place around each: name, cell, zone radius in
        /// cells. Ids are their index: only add at the end.
        /// </summary>
        public static readonly (string name, Vector3Int cell, float zoneRadius)[] Places =
        {
            ("Pilgrim's Refuge", new Vector3Int(19, 78, 0), 12f),
            ("Merchant's Way", new Vector3Int(62, 38, 0), 22f),
            ("Fields of Dreams", new Vector3Int(185, 61, 0), 32f),
            ("Howler's Valley", new Vector3Int(103, 123, 0), 26f),
            ("Thicket of the Wanderer", new Vector3Int(193, 200, 0), 28f),
            ("St. Revere", new Vector3Int(526, 252, 0), 30f),
            ("Kandorys Passage", new Vector3Int(324, 139, 0), 30f),
            ("Kandorys Castle", new Vector3Int(274, 284, 0), 20f),
        };

        /// <summary>Named places without a waystone.</summary>
        public static readonly (string name, Vector3Int cell, float zoneRadius)[] OtherZones =
        {
            ("Wolf's Den", new Vector3Int(79, 139, 0), 8f),
            ("Trog Cave", new Vector3Int(241, 26, 0), 10f),
            ("Abandoned Barracks", new Vector3Int(267, 191, 0), 10f),
            ("Telamont's Demiplane", new Vector3Int(370, 272, 0), 10f),
            ("Desecrated Altar", new Vector3Int(508, 143, 0), 10f),
        };
    }
}
