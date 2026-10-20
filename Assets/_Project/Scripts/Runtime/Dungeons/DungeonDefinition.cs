using System;
using RPG.Items;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.Dungeons
{
    /// <summary>A monster standing at a spot of the dungeon's plan; it does not come back once killed.</summary>
    [Serializable]
    public struct DungeonMonsterSpot
    {
        public GameObject prefab;
        public Vector3Int cell;
        public string displayName;
        [Min(1)] public int level;
        [Tooltip("Tougher, bigger, rewards more (the source game's elites and boss-flagged monsters).")]
        public bool elite;
        [Tooltip("A boss: the dungeon counts as beaten when all its bosses are dead.")]
        public bool boss;
        [Tooltip("Health as a multiple of a normal monster of its level (0: the usual, or an elite's).")]
        [Min(0f)] public float healthScale;
        [Tooltip("Damage as a multiple of a normal monster's (0: the usual, or an elite's).")]
        [Min(0f)] public float damageScale;
        [Tooltip("Gold reward multiple.")]
        [Min(0f)] public float goldScale;
        [Tooltip("Rolled once on death on top of its usual drops (keys, treasures, gems).")]
        public LootEntry[] loot;
        [Tooltip("Chances of a piece of gear of each quality (the source game's boss loot chances).")]
        [Range(0f, 1f)] public float blueChance;
        [Range(0f, 1f)] public float goldChance;
        [Range(0f, 1f)] public float purpleChance;
        [Tooltip("Not stunned, incapacitated or rooted (the source game's mechanic immunity, mostly bosses).")]
        public bool controlImmune;
    }

    /// <summary>A container at a spot of the dungeon's plan; opened once per instance.</summary>
    [Serializable]
    public struct DungeonChestSpot
    {
        public GameObject prefab;
        public Vector3Int cell;
        [Min(1)] public int level;
    }

    /// <summary>A portal out of the dungeon, to a point in the world.</summary>
    [Serializable]
    public struct DungeonExit
    {
        public Vector3Int cell;
        public Vector2 destination;
        public string label;
    }

    /// <summary>A portal inside the dungeon into another dungeon (which needs its own key).</summary>
    [Serializable]
    public struct DungeonDoor
    {
        public Vector3Int cell;
        public DungeonDefinition target;
    }

    /// <summary>
    /// Everything a dungeon is made of: its plan (the source game's map: which cells are walkable), where players arrive,
    /// its monsters, containers, exits and doors into deeper dungeons, and who may enter.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Dungeons/Dungeon", fileName = "Dungeon")]
    public class DungeonDefinition : ScriptableObject
    {
        public string displayName = "Dungeon";
        [Tooltip("Network prefab with a DungeonInstance; one is spawned per group.")]
        public GameObject instancePrefab;
        public TileBase floorTile;
        [Tooltip("Must have a collider (Tile collider type Grid).")]
        public TileBase wallTile;
        public Sprite portalSprite;

        [Header("Plan")]
        [Tooltip("Row y, column x: '.' is walkable floor, anything else is not.")]
        public string[] rows = Array.Empty<string>();
        public Vector3Int arrival;
        public DungeonExit[] exits = Array.Empty<DungeonExit>();
        public DungeonDoor[] doors = Array.Empty<DungeonDoor>();

        [Header("Inside")]
        public DungeonMonsterSpot[] monsters = Array.Empty<DungeonMonsterSpot>();
        public DungeonChestSpot[] chests = Array.Empty<DungeonChestSpot>();
        [Tooltip("A normal monster: the health and damage multiples of the spots are measured against it.")]
        public GameObject referenceMonster;

        [Header("Entry")]
        [Tooltip("Players below this level cannot enter.")]
        [Min(1)] public int minLevel = 1;
        [Tooltip("The player who opens an instance must carry one; it is used up. Leave empty for no key.")]
        public ItemDefinition key;
        [Tooltip("Most players one instance takes in.")]
        [Min(1)] public int maxPlayers = 4;

        [Tooltip("Monsters in the dungeon attack players who come this close (unlike the outdoor ones for now).")]
        [Min(0f)] public float monsterAggroRadius = 3.5f;

        /// <summary>Its plan as cells.</summary>
        public DungeonLayout Layout => DungeonLayout.For(this);
    }
}
