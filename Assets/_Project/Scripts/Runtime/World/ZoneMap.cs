using System;
using System.Collections.Generic;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// Where a map's named areas are, block by block like the source game's zones (13 x 13 cells each), and the labels of
    /// its world map (M): the places with their level ranges and the elites and bosses. <see cref="Zone.NameAt"/> asks it first.
    /// </summary>
    public class ZoneMap : MonoBehaviour
    {
        [Serializable]
        public struct Label
        {
            public string name;
            /// <summary>The line under the name ("Level 5+"), or empty.</summary>
            public string detail;
            public Vector2 position;
            /// <summary>A town: written larger.</summary>
            public bool town;
            /// <summary>An elite or boss (a skull on the map; <see cref="detail"/> its level).</summary>
            public bool boss;
        }

        [SerializeField] private Grid grid;
        [SerializeField, Min(1)] private int blockSize = 13;
        [SerializeField] private string[] zoneNames = Array.Empty<string>();
        [Tooltip("Rows of blocks (row y / blockSize, column x / blockSize): 'A' is the first zone name, '.' none.")]
        [SerializeField] private string[] blocks = Array.Empty<string>();
        [SerializeField] private Label[] labels = Array.Empty<Label>();

        private static readonly List<ZoneMap> Maps = new List<ZoneMap>();

        public static ListView<ZoneMap> All => new ListView<ZoneMap>(Maps);
        /// <summary>The labels (the array itself, so walking it allocates nothing).</summary>
        public Label[] Labels => labels;

        public void Configure(Grid cells, int size, string[] names, string[] blockRows, Label[] mapLabels)
        {
            grid = cells;
            blockSize = Mathf.Max(1, size);
            zoneNames = names ?? Array.Empty<string>();
            blocks = blockRows ?? Array.Empty<string>();
            labels = mapLabels ?? Array.Empty<Label>();
        }

        private void OnEnable() => Maps.Add(this);

        private void OnDisable() => Maps.Remove(this);

        /// <summary>The zone at a world position, or null outside every zone of this map.</summary>
        public string NameAt(Vector2 position)
        {
            if (grid == null) return null;
            Vector3Int cell = grid.WorldToCell(position);
            if (cell.x < 0 || cell.y < 0) return null;
            int row = cell.y / blockSize;
            int column = cell.x / blockSize;
            if (row >= blocks.Length || column >= blocks[row].Length) return null;
            int index = blocks[row][column] - 'A';
            return index >= 0 && index < zoneNames.Length ? zoneNames[index] : null;
        }
    }
}
