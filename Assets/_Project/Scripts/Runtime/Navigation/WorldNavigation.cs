using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.Navigation
{
    /// <summary>
    /// Path finding on the world map. A cell is walkable when it has ground, no obstacle tile and
    /// no static prop (tree trunk, training dummy post) on it. The grid is built the first time it is needed,
    /// after the scene has loaded. Paths are smoothed so characters walk straight wherever nothing is in the way.
    /// Used by the server for monsters and by the local player for click-to-move.
    /// Several maps can exist at once (the world and dungeon instances far away from it); <see cref="At"/> finds
    /// the one under a position.
    /// </summary>
    public class WorldNavigation : MonoBehaviour
    {
        [SerializeField] private Grid grid;
        [SerializeField] private Tilemap ground;
        [SerializeField] private Tilemap obstacles;
        [Tooltip("Shown above the minimap.")]
        [SerializeField] private string zoneName = "Wilderness";
        [Tooltip("A cell is blocked when a solid collider without a rigidbody (tree trunk, post) overlaps this radius around its center.")]
        [SerializeField, Min(0.01f)] private float propCheckRadius = 0.15f;
        [Tooltip("How far from blocked cells a straight walk must stay (about the radius of a character's feet).")]
        [SerializeField, Min(0f)] private float clearance = 0.1f;

        private const float LineSampleStep = 0.08f;
        private const int NearestWalkableSearchRadius = 3;

        private NavGrid navGrid;
        private Rect worldBounds;
        private readonly List<Vector3Int> cellPath = new List<Vector3Int>();
        private readonly List<Vector2> points = new List<Vector2>();
        private readonly List<Collider2D> overlaps = new List<Collider2D>();

        private static readonly List<WorldNavigation> Maps = new List<WorldNavigation>();

        /// <summary>The map that contains a world position, or null when the position is on no map (tests, the void).</summary>
        public static WorldNavigation At(Vector2 worldPosition)
        {
            foreach (WorldNavigation map in Maps)
                if (map != null && map.Contains(worldPosition)) return map;
            return null;
        }

        public string ZoneName
        {
            get => zoneName;
            set => zoneName = value;
        }

        /// <summary>World-space rectangle around every ground cell.</summary>
        public Rect WorldBounds
        {
            get
            {
                if (navGrid == null) Build();
                return worldBounds;
            }
        }

        private NavGrid Map
        {
            get
            {
                if (navGrid == null) Build();
                return navGrid;
            }
        }

        private void OnEnable()
        {
            if (!Maps.Contains(this)) Maps.Add(this);
        }

        private void OnDisable()
        {
            Maps.Remove(this);
        }

        /// <summary>True when the position is inside this map's ground area (walkable or not).</summary>
        public bool Contains(Vector2 worldPosition)
        {
            return Map.Contains(ToCell(worldPosition));
        }

        /// <summary>Sets the map to navigate (used by the scene builder and tests). Rebuilds on next use.</summary>
        public void Configure(Grid worldGrid, Tilemap groundTilemap, Tilemap obstacleTilemap)
        {
            grid = worldGrid;
            ground = groundTilemap;
            obstacles = obstacleTilemap;
            navGrid = null;
        }

        /// <summary>Reads the tilemaps and props again (for example after the map changed).</summary>
        public void Build()
        {
            if (grid == null) grid = GetComponentInParent<Grid>();
            if (grid == null || ground == null)
            {
                navGrid = new NavGrid(new BoundsInt(0, 0, 0, 0, 0, 1), _ => false, _ => Vector2.zero);
                Debug.LogWarning("[Navigation] No grid or ground tilemap set; nothing is walkable.");
                return;
            }

            Physics2D.SyncTransforms();
            ground.CompressBounds();
            navGrid = new NavGrid(ground.cellBounds, IsCellWalkable, cell => grid.GetCellCenterWorld(cell));
            worldBounds = CellsToWorld(ground.cellBounds);
        }

        private bool IsCellWalkable(Vector3Int cell)
        {
            if (!ground.HasTile(cell)) return false;
            if (obstacles != null && obstacles.HasTile(cell)) return false;
            return !HasStaticProp(grid.GetCellCenterWorld(cell));
        }

        private bool HasStaticProp(Vector2 point)
        {
            Physics2D.OverlapCircle(point, propCheckRadius, ContactFilter2D.noFilter, overlaps);
            foreach (Collider2D hit in overlaps)
            {
                // Tilemap colliders are covered by the obstacle tiles; anything with a rigidbody moves (characters).
                if (hit.isTrigger || hit.attachedRigidbody != null || hit is TilemapCollider2D || hit is CompositeCollider2D) continue;
                return true;
            }
            return false;
        }

        private Rect CellsToWorld(BoundsInt cells)
        {
            Vector2 min = Vector2.positiveInfinity;
            Vector2 max = Vector2.negativeInfinity;
            foreach (int x in new[] { cells.xMin, cells.xMax - 1 })
            foreach (int y in new[] { cells.yMin, cells.yMax - 1 })
            {
                Vector2 center = grid.GetCellCenterWorld(new Vector3Int(x, y, 0));
                min = Vector2.Min(min, center);
                max = Vector2.Max(max, center);
            }
            Vector2 half = (Vector2)grid.cellSize * 0.5f;
            return Rect.MinMaxRect(min.x - half.x, min.y - half.y, max.x + half.x, max.y + half.y);
        }

        public bool IsWalkable(Vector2 worldPosition)
        {
            return Map.IsWalkable(ToCell(worldPosition));
        }

        /// <summary>
        /// True when a character can walk in a straight line from <paramref name="from"/> to <paramref name="to"/>
        /// without its feet entering a blocked cell.
        /// </summary>
        public bool HasClearLine(Vector2 from, Vector2 to)
        {
            NavGrid map = Map;
            Vector2 delta = to - from;
            float length = delta.magnitude;
            Vector2 side = length > 0.0001f ? new Vector2(-delta.y, delta.x) / length * clearance : Vector2.zero;
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / LineSampleStep));

            // The first sample is skipped: a character may stand at the edge of a blocked cell (next to a tree).
            for (int i = 1; i <= steps; i++)
            {
                Vector2 point = from + delta * ((float)i / steps);
                if (!map.IsWalkable(ToCell(point))) return false;
                if (clearance > 0f && (!map.IsWalkable(ToCell(point + side)) || !map.IsWalkable(ToCell(point - side))))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Finds a walkable route. <paramref name="waypoints"/> receives the points to walk through in order
        /// (not including <paramref name="from"/>). When <paramref name="to"/> is blocked (a rock, a tree),
        /// the route ends at the nearest walkable cell instead. Returns false when there is no route.
        /// </summary>
        public bool FindPath(Vector2 from, Vector2 to, List<Vector2> waypoints)
        {
            waypoints.Clear();
            NavGrid map = Map;

            Vector3Int goalCell = ToCell(to);
            Vector2 goal = to;
            if (!map.IsWalkable(goalCell))
            {
                if (!map.TryFindNearestWalkable(goalCell, NearestWalkableSearchRadius, out goalCell)) return false;
                goal = map.CellCenter(goalCell);
            }

            if (HasClearLine(from, goal))
            {
                waypoints.Add(goal);
                return true;
            }

            if (!map.FindPath(ToCell(from), goalCell, cellPath)) return false;

            points.Clear();
            foreach (Vector3Int cell in cellPath)
                points.Add(map.CellCenter(cell));
            if (points.Count == 0) points.Add(goal);
            else points[points.Count - 1] = goal;

            // Skip every waypoint that can be reached in a straight line.
            Vector2 current = from;
            int index = 0;
            while (index < points.Count)
            {
                int farthest = index;
                for (int candidate = points.Count - 1; candidate > index; candidate--)
                {
                    if (HasClearLine(current, points[candidate]))
                    {
                        farthest = candidate;
                        break;
                    }
                }
                waypoints.Add(points[farthest]);
                current = points[farthest];
                index = farthest + 1;
            }
            return true;
        }

        /// <summary>A random walkable point within <paramref name="radius"/> of <paramref name="center"/> (squashed for the isometric ground).</summary>
        public bool TryGetRandomPoint(Vector2 center, float radius, out Vector2 point)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                point = center + new Vector2(offset.x, offset.y * 0.5f);
                if (IsWalkable(point)) return true;
            }
            point = center;
            return IsWalkable(center);
        }

        private Vector3Int ToCell(Vector2 worldPosition)
        {
            Vector3Int cell = grid != null ? grid.WorldToCell(worldPosition) : Vector3Int.zero;
            cell.z = 0;
            return cell;
        }
    }
}
