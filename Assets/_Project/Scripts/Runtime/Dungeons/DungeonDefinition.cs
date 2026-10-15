using System;
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

        [Tooltip("Monsters in the dungeon attack players who come this close (unlike the outdoor ones for now).")]
        [Min(0f)] public float monsterAggroRadius = 3.5f;
    }
}
