using System.Collections.Generic;
using NUnit.Framework;
using RPG.Navigation;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.Tests
{
    /// <summary>A* on a cell grid and path finding on real isometric tilemaps.</summary>
    public class NavigationTests
    {
        private static Vector2 IsometricCenter(Vector3Int cell) => new Vector2((cell.x - cell.y) * 0.5f, (cell.x + cell.y) * 0.25f);

        private readonly List<GameObject> created = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            // Immediately, so the test maps are gone before the next test runs.
            foreach (GameObject go in created)
                Object.DestroyImmediate(go);
            created.Clear();
        }

        [Test]
        public void NavGrid_FindsPathThroughGapInWall()
        {
            var wall = new HashSet<Vector3Int>();
            for (int y = 0; y < 9; y++) wall.Add(new Vector3Int(5, y, 0));
            var grid = new NavGrid(new BoundsInt(0, 0, 0, 10, 10, 1), cell => !wall.Contains(cell), IsometricCenter);

            var path = new List<Vector3Int>();
            Assert.IsTrue(grid.FindPath(new Vector3Int(2, 2, 0), new Vector3Int(8, 2, 0), path));
            Assert.AreEqual(new Vector3Int(8, 2, 0), path[path.Count - 1]);
            Assert.IsTrue(path.Contains(new Vector3Int(5, 9, 0)), "The only way is through the gap.");

            Vector3Int previous = new Vector3Int(2, 2, 0);
            foreach (Vector3Int cell in path)
            {
                Assert.IsTrue(grid.IsWalkable(cell));
                Assert.LessOrEqual(Mathf.Abs(cell.x - previous.x), 1);
                Assert.LessOrEqual(Mathf.Abs(cell.y - previous.y), 1);
                previous = cell;
            }
        }

        [Test]
        public void NavGrid_NoPathWhenWalledOff()
        {
            var grid = new NavGrid(new BoundsInt(0, 0, 0, 10, 10, 1), cell => cell.x != 5, IsometricCenter);
            var path = new List<Vector3Int>();
            Assert.IsFalse(grid.FindPath(new Vector3Int(2, 2, 0), new Vector3Int(8, 2, 0), path));
            Assert.IsFalse(grid.FindPath(new Vector3Int(2, 2, 0), new Vector3Int(5, 2, 0), path), "Blocked goal.");
        }

        [Test]
        public void NavGrid_DoesNotCutBlockedCorners()
        {
            var blocked = new HashSet<Vector3Int> { new Vector3Int(1, 0, 0), new Vector3Int(0, 1, 0) };
            var grid = new NavGrid(new BoundsInt(0, 0, 0, 2, 2, 1), cell => !blocked.Contains(cell), IsometricCenter);
            Assert.IsFalse(grid.FindPath(new Vector3Int(0, 0, 0), new Vector3Int(1, 1, 0), new List<Vector3Int>()));

            var oneBlocked = new NavGrid(new BoundsInt(0, 0, 0, 2, 2, 1), cell => cell != new Vector3Int(1, 0, 0), IsometricCenter);
            var path = new List<Vector3Int>();
            Assert.IsTrue(oneBlocked.FindPath(new Vector3Int(0, 0, 0), new Vector3Int(1, 1, 0), path));
            Assert.AreEqual(2, path.Count, "Goes around the blocked corner instead of clipping it.");
        }

        [Test]
        public void NavGrid_FindsNearestWalkableCell()
        {
            var grid = new NavGrid(new BoundsInt(0, 0, 0, 10, 10, 1), cell => cell.x != 5, IsometricCenter);
            Assert.IsTrue(grid.TryFindNearestWalkable(new Vector3Int(5, 3, 0), 3, out Vector3Int nearest));
            Assert.IsTrue(grid.IsWalkable(nearest));
            Assert.AreEqual(1, Mathf.Abs(nearest.x - 5));
        }

        [Test]
        public void WorldNavigation_WalksAroundObstacleTiles()
        {
            WorldNavigation navigation = CreateMap(out Grid grid, out Tilemap obstacles);
            // A wall across the middle of a 12x12 map with a gap at the top end.
            for (int y = 0; y < 10; y++)
                obstacles.SetTile(new Vector3Int(6, y, 0), ScriptableObject.CreateInstance<Tile>());

            Vector2 from = grid.GetCellCenterWorld(new Vector3Int(3, 3, 0));
            Vector2 to = grid.GetCellCenterWorld(new Vector3Int(9, 3, 0));
            Assert.IsFalse(navigation.HasClearLine(from, to), "The wall is in the way.");

            var waypoints = new List<Vector2>();
            Assert.IsTrue(navigation.FindPath(from, to, waypoints));
            Assert.Greater(waypoints.Count, 1, "Has to turn at least once.");
            Assert.AreEqual(to, waypoints[waypoints.Count - 1]);

            Vector2 current = from;
            foreach (Vector2 waypoint in waypoints)
            {
                Assert.IsTrue(navigation.HasClearLine(current, waypoint), $"Straight segment {current} -> {waypoint}");
                current = waypoint;
            }
        }

        [Test]
        public void WorldNavigation_BlockedDestinationEndsNextToIt()
        {
            WorldNavigation navigation = CreateMap(out Grid grid, out Tilemap obstacles);
            var rock = new Vector3Int(8, 8, 0);
            obstacles.SetTile(rock, ScriptableObject.CreateInstance<Tile>());

            Vector2 from = grid.GetCellCenterWorld(new Vector3Int(3, 3, 0));
            var waypoints = new List<Vector2>();
            Assert.IsTrue(navigation.FindPath(from, grid.GetCellCenterWorld(rock), waypoints));

            Vector2 end = waypoints[waypoints.Count - 1];
            Assert.IsTrue(navigation.IsWalkable(end));
            Assert.Less(Vector2.Distance(end, grid.GetCellCenterWorld(rock)), 1f);
        }

        [Test]
        public void LineOfSight_IsBlockedByWallsOnly()
        {
            WorldNavigation navigation = CreateMap(out Grid grid, out Tilemap obstacles);
            Tile wall = ScriptableObject.CreateInstance<Tile>();
            Tile water = ScriptableObject.CreateInstance<Tile>();
            for (int y = 0; y < 12; y++) obstacles.SetTile(new Vector3Int(6, y, 0), wall);
            obstacles.SetTile(new Vector3Int(3, 8, 0), water);
            navigation.Configure(grid, navigation.GetComponentInChildren<Tilemap>(), obstacles, new TileBase[] { wall });

            Vector2 left = grid.GetCellCenterWorld(new Vector3Int(3, 5, 0));
            Vector2 right = grid.GetCellCenterWorld(new Vector3Int(9, 5, 0));
            Assert.IsFalse(navigation.HasLineOfSight(left, right), "A wall is in between.");
            Assert.IsFalse(navigation.HasLineOfSight(right, left));

            Vector2 acrossWater = grid.GetCellCenterWorld(new Vector3Int(3, 10, 0));
            Assert.IsTrue(navigation.HasLineOfSight(left, acrossWater), "Water does not block sight.");

            // Standing right at the wall still sees the next cell along it.
            Vector2 atWall = grid.GetCellCenterWorld(new Vector3Int(5, 5, 0));
            Assert.IsTrue(navigation.HasLineOfSight(left, atWall));
        }

        private WorldNavigation CreateMap(out Grid grid, out Tilemap obstacles)
        {
            grid = new GameObject("Test Grid").AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            created.Add(grid.gameObject);

            Tilemap ground = CreateTilemap(grid, "Ground");
            obstacles = CreateTilemap(grid, "Obstacles");
            Tile groundTile = ScriptableObject.CreateInstance<Tile>();
            for (int x = 0; x < 12; x++)
            for (int y = 0; y < 12; y++)
                ground.SetTile(new Vector3Int(x, y, 0), groundTile);

            var navigation = grid.gameObject.AddComponent<WorldNavigation>();
            navigation.Configure(grid, ground, obstacles);
            return navigation;
        }

        private static Tilemap CreateTilemap(Grid grid, string tilemapName)
        {
            var go = new GameObject(tilemapName);
            go.transform.SetParent(grid.transform, false);
            return go.AddComponent<Tilemap>();
        }
    }
}
