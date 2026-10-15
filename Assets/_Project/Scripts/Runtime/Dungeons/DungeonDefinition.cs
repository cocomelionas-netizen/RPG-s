using System;
using RPG.Items;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.Dungeons
{
    /// <summary>A group of monsters waiting in one room of the dungeon.</summary>
    [Serializable]
    public struct MonsterPack
    {
        public GameObject prefab;
        [Tooltip("Room index along DungeonLayout.Rooms (0 is the entrance).")]
        public int room;
        [Min(1)] public int count;

        public MonsterPack(GameObject prefab, int room, int count)
        {
            this.prefab = prefab;
            this.room = room;
            this.count = count;
        }
    }

    /// <summary>Everything a dungeon is made of: its look, its monsters and its boss.</summary>
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

        public MonsterPack[] packs = Array.Empty<MonsterPack>();
        public GameObject bossPrefab;

        [Header("Entry")]
        [Tooltip("Players below this level cannot enter.")]
        [Min(1)] public int minLevel = 1;
        [Tooltip("The player who opens an instance must carry one; it is used up. Leave empty for no key.")]
        public ItemDefinition key;
        [Tooltip("Most players one instance takes in.")]
        [Min(1)] public int maxPlayers = 4;

        [Tooltip("Monsters in the dungeon attack players who come this close (unlike the outdoor ones for now).")]
        [Min(0f)] public float monsterAggroRadius = 3.5f;
    }
}
