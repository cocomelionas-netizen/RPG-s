using System;
using System.IO;
using System.Linq;
using RPG.CameraSystem;
using RPG.Characters;
using RPG.Controls;
using RPG.Player;
using RPG.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;

namespace RPG.EditorTools
{
    /// <summary>
    /// Builds the test scene: 2D renderer setup, placeholder art and tiles, an isometric
    /// test map with rocks and trees, the animated player prefab, the click marker and the follow camera.
    /// Menu: RPG > Build Test Scene. Safe to run again (it regenerates everything).
    /// </summary>
    public static class TestSceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/World_Test.unity";
        private const string TilesFolder = "Assets/_Project/Tiles";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string TreePrefabPath = "Assets/_Project/Prefabs/World/Tree.prefab";
        private const string PlayerAnimationSetPath = "Assets/_Project/Data/Characters/Player_AnimationSet.asset";
        private const string PlayerArtFolder = PlaceholderArtGenerator.Folder + "/Player";
        private const string Renderer2DPath = "Assets/_Project/Settings/Rendering/Renderer2D.asset";
        private static readonly string[] PipelineAssetPaths =
        {
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/Mobile_RPAsset.asset",
        };

        private const int MapSize = 40;
        private const int Seed = 12345;
        private const int TreeSeed = 777;

        [MenuItem("RPG/Build Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;

            Setup2DRendering();

            Tile[] grassTiles =
            {
                CreateTile("Grass_A", PlaceholderArtGenerator.GroundTile("Grass_A", new Color(0.27f, 0.42f, 0.2f), new Color(0.42f, 0.55f, 0.26f), 1)),
                CreateTile("Grass_B", PlaceholderArtGenerator.GroundTile("Grass_B", new Color(0.25f, 0.4f, 0.19f), new Color(0.38f, 0.5f, 0.22f), 2)),
                CreateTile("Grass_C", PlaceholderArtGenerator.GroundTile("Grass_C", new Color(0.3f, 0.44f, 0.21f), new Color(0.47f, 0.56f, 0.3f), 3)),
            };
            Tile pathTile = CreateTile("Path", PlaceholderArtGenerator.GroundTile("Path", new Color(0.5f, 0.41f, 0.29f), new Color(0.64f, 0.55f, 0.4f), 4));
            Tile rockTile = CreateTile("Rock", PlaceholderArtGenerator.Rock("Rock", 5), Tile.ColliderType.Grid);
            Sprite treeSprite = PlaceholderArtGenerator.Tree("Tree", 6);
            Sprite markerSprite = PlaceholderArtGenerator.GroundRing("ClickMarker", new Color(1f, 0.85f, 0.4f, 0.9f));
            CharacterAnimationSet playerAnimations = CreatePlayerAnimationSet(new Color(0.2f, 0.32f, 0.62f));
            GameObject treePrefab = CreateTreePrefab(treeSprite);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Grid grid = CreateGrid();
            Tilemap ground = CreateTilemap(grid, "Ground", -100, TilemapRenderer.Mode.Chunk);
            Tilemap obstacles = CreateTilemap(grid, "Obstacles", 0, TilemapRenderer.Mode.Individual);
            obstacles.gameObject.AddComponent<TilemapCollider2D>();
            PaintTestMap(ground, obstacles, grassTiles, pathTile, rockTile);
            PlaceTrees(grid, obstacles, treePrefab);

            CreateGlobalLight();
            CreateEventSystem();

            GameObject playerPrefab = CreatePlayerPrefab(playerAnimations);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            Vector3Int center = new Vector3Int(MapSize / 2, MapSize / 2, 0);
            player.transform.position = grid.GetCellCenterWorld(center);

            CreateCamera(player.transform);
            CreateClickMarker(markerSprite, player.GetComponent<PlayerMovement>());

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            AssetDatabase.SaveAssets();

            Debug.Log($"[TestScene] Test scene built: {ScenePath}");
        }

        /// <summary>Batch mode entry: Unity.exe -batchmode -projectPath . -executeMethod RPG.EditorTools.TestSceneBuilder.BuildFromCommandLine -quit</summary>
        public static void BuildFromCommandLine()
        {
            Build();
            string screenshot = GetArgument("-screenshotPath");
            if (!string.IsNullOrEmpty(screenshot))
                CaptureScreenshot(screenshot);
        }

        private static void Setup2DRendering()
        {
            // The 2D Renderer is required for sprite lighting and correct 2D sorting in URP.
            var renderer2D = AssetDatabase.LoadAssetAtPath<Renderer2DData>(Renderer2DPath);
            if (renderer2D == null)
            {
                renderer2D = ScriptableObject.CreateInstance<Renderer2DData>();
                AssetDatabase.CreateAsset(renderer2D, Renderer2DPath);
            }

            // Isometric sorting: objects lower on screen (smaller Y) are drawn in front.
            var rendererObject = new SerializedObject(renderer2D);
            rendererObject.FindProperty("m_TransparencySortMode").intValue = (int)TransparencySortMode.CustomAxis;
            rendererObject.FindProperty("m_TransparencySortAxis").vector3Value = new Vector3(0f, 1f, 0f);
            rendererObject.ApplyModifiedPropertiesWithoutUndo();

            GraphicsSettings.transparencySortMode = TransparencySortMode.CustomAxis;
            GraphicsSettings.transparencySortAxis = new Vector3(0f, 1f, 0f);

            foreach (string path in PipelineAssetPaths)
            {
                var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
                if (pipeline == null) continue;

                var pipelineObject = new SerializedObject(pipeline);
                SerializedProperty renderers = pipelineObject.FindProperty("m_RendererDataList");
                if (renderers.arraySize == 0) renderers.arraySize = 1;
                renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer2D;
                pipelineObject.FindProperty("m_DefaultRendererIndex").intValue = 0;
                pipelineObject.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(pipeline);
            }
        }

        private static Tile CreateTile(string name, Sprite sprite, Tile.ColliderType colliderType = Tile.ColliderType.None)
        {
            Directory.CreateDirectory(TilesFolder);
            string path = $"{TilesFolder}/{name}.asset";
            var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<Tile>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            tile.colliderType = colliderType;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        private static Grid CreateGrid()
        {
            var grid = new GameObject("Grid").AddComponent<Grid>();
            grid.cellLayout = GridLayout.CellLayout.Isometric;
            grid.cellSize = new Vector3(1f, 0.5f, 1f);
            return grid;
        }

        private static Tilemap CreateTilemap(Grid grid, string name, int sortingOrder, TilemapRenderer.Mode mode)
        {
            var go = new GameObject(name);
            go.transform.SetParent(grid.transform, false);
            var tilemap = go.AddComponent<Tilemap>();
            tilemap.tileAnchor = new Vector3(0.5f, 0.5f, 0f);
            var tilemapRenderer = go.AddComponent<TilemapRenderer>();
            tilemapRenderer.sortingOrder = sortingOrder;
            tilemapRenderer.mode = mode;
            return tilemap;
        }

        private static void PaintTestMap(Tilemap ground, Tilemap obstacles, Tile[] grassTiles, Tile pathTile, Tile rockTile)
        {
            var random = new System.Random(Seed);
            int center = MapSize / 2;

            for (int x = 0; x < MapSize; x++)
            for (int y = 0; y < MapSize; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool isPath = x == center || y == center;
                ground.SetTile(cell, isPath ? pathTile : grassTiles[random.Next(grassTiles.Length)]);

                bool isBorder = x == 0 || y == 0 || x == MapSize - 1 || y == MapSize - 1;
                bool nearSpawn = Mathf.Abs(x - center) <= 3 && Mathf.Abs(y - center) <= 3;
                bool scatteredRock = !isPath && !nearSpawn && random.NextDouble() < 0.05;
                if (isBorder || scatteredRock)
                    obstacles.SetTile(cell, rockTile);
            }

            // A small wall near the spawn to test collisions and sorting (walk behind / in front of it).
            for (int i = 0; i < 4; i++)
                obstacles.SetTile(new Vector3Int(center + 3 + i, center + 2, 0), rockTile);
        }

        private static void CreateGlobalLight()
        {
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;
            light.intensity = 1f;
        }

        private static void CreateEventSystem()
        {
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
        }

        private static GameObject CreatePlayerPrefab(CharacterAnimationSet animations)
        {
            var player = new GameObject("Player");

            var spriteRenderer = player.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = animations.FindClip(CharacterAnimationSet.IdleClip).south[0];
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

            var body = player.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.freezeRotation = true;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            // Small collider at the feet: only the footprint collides in an isometric game.
            var feet = player.AddComponent<CircleCollider2D>();
            feet.radius = 0.12f;
            feet.offset = new Vector2(0f, 0.04f);

            player.AddComponent<PlayerInputReader>();
            player.AddComponent<PlayerMovement>();
            player.AddComponent<CharacterAnimator>().AnimationSet = animations;

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
            return prefab;
        }

        private static readonly (Direction8 direction, Vector2 facing)[] DrawnDirections =
        {
            // West-facing directions are mirrored from these at runtime.
            (Direction8.East, new Vector2(1f, 0f)),
            (Direction8.NorthEast, new Vector2(0.707f, 0.707f)),
            (Direction8.North, new Vector2(0f, 1f)),
            (Direction8.SouthEast, new Vector2(0.707f, -0.707f)),
            (Direction8.South, new Vector2(0f, -1f)),
        };

        private static CharacterAnimationSet CreatePlayerAnimationSet(Color cloakColor)
        {
            float[] idleBob = { 0f, 1.5f };
            float[] walkBob = { 0f, 3f, 0f, 3f };
            float[] walkStride = { 1f, 0f, -1f, 0f };

            var idle = new DirectionalClip { id = CharacterAnimationSet.IdleClip, framesPerSecond = 2f, loop = true };
            var walk = new DirectionalClip { id = CharacterAnimationSet.WalkClip, framesPerSecond = 8f, loop = true };

            foreach ((Direction8 direction, Vector2 facing) in DrawnDirections)
            {
                var idleFrames = new Sprite[idleBob.Length];
                for (int i = 0; i < idleFrames.Length; i++)
                    idleFrames[i] = PlaceholderArtGenerator.CharacterFrame(PlayerArtFolder, $"Player_Idle_{direction}_{i}", cloakColor, facing, idleBob[i], 0f);

                var walkFrames = new Sprite[walkBob.Length];
                for (int i = 0; i < walkFrames.Length; i++)
                    walkFrames[i] = PlaceholderArtGenerator.CharacterFrame(PlayerArtFolder, $"Player_Walk_{direction}_{i}", cloakColor, facing, walkBob[i], walkStride[i]);

                SetFrames(idle, direction, idleFrames);
                SetFrames(walk, direction, walkFrames);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerAnimationSetPath));
            var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(PlayerAnimationSetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
                AssetDatabase.CreateAsset(set, PlayerAnimationSetPath);
            }
            set.MirrorWestFromEast = true;
            set.Clips = new[] { idle, walk };
            EditorUtility.SetDirty(set);
            return set;
        }

        private static void SetFrames(DirectionalClip clip, Direction8 direction, Sprite[] frames)
        {
            switch (direction)
            {
                case Direction8.East: clip.east = frames; break;
                case Direction8.NorthEast: clip.northEast = frames; break;
                case Direction8.North: clip.north = frames; break;
                case Direction8.NorthWest: clip.northWest = frames; break;
                case Direction8.West: clip.west = frames; break;
                case Direction8.SouthWest: clip.southWest = frames; break;
                case Direction8.South: clip.south = frames; break;
                case Direction8.SouthEast: clip.southEast = frames; break;
            }
        }

        private static GameObject CreateTreePrefab(Sprite sprite)
        {
            var tree = new GameObject("Tree");
            var spriteRenderer = tree.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

            // Only the trunk blocks movement; the canopy is walked behind.
            var trunk = tree.AddComponent<CircleCollider2D>();
            trunk.radius = 0.08f;
            trunk.offset = new Vector2(0f, 0.04f);

            Directory.CreateDirectory(Path.GetDirectoryName(TreePrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(tree, TreePrefabPath);
            UnityEngine.Object.DestroyImmediate(tree);
            return prefab;
        }

        private static void PlaceTrees(Grid grid, Tilemap obstacles, GameObject treePrefab)
        {
            var random = new System.Random(TreeSeed);
            var props = new GameObject("Props").transform;
            int center = MapSize / 2;

            for (int x = 2; x < MapSize - 2; x++)
            for (int y = 2; y < MapSize - 2; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool isPath = x == center || y == center;
                bool nearSpawn = Mathf.Abs(x - center) <= 2 && Mathf.Abs(y - center) <= 2;
                if (isPath || nearSpawn || obstacles.HasTile(cell) || random.NextDouble() >= 0.03)
                    continue;

                var tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, props);
                tree.transform.position = grid.GetCellCenterWorld(cell);
            }

            // One tree right next to the spawn to test walking behind and in front of tall objects.
            var nearTree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, props);
            nearTree.transform.position = grid.GetCellCenterWorld(new Vector3Int(center - 2, center + 1, 0));
        }

        private static void CreateClickMarker(Sprite sprite, PlayerMovement movement)
        {
            var go = new GameObject("Click Marker");
            var spriteRenderer = go.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            // Above the ground, below characters and props.
            spriteRenderer.sortingOrder = -50;

            var marker = go.AddComponent<ClickMarker>();
            var markerObject = new SerializedObject(marker);
            markerObject.FindProperty("movement").objectReferenceValue = movement;
            markerObject.FindProperty("markerRenderer").objectReferenceValue = spriteRenderer;
            markerObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateCamera(Transform target)
        {
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 4.5f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.06f, 0.07f, 0.09f);
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 100f;
            go.transform.position = new Vector3(target.position.x, target.position.y, -10f);
            go.AddComponent<AudioListener>();
            go.AddComponent<UniversalAdditionalCameraData>();
            go.AddComponent<CameraFollow>().Follow(target, snap: true);
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes.Where(s => s.path != ScenePath).ToList();
            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        /// <summary>Renders the main camera of the open scene to a PNG (used to verify the build in batch mode).</summary>
        public static void CaptureScreenshot(string path)
        {
            Camera cam = Camera.main;
            var renderTexture = new RenderTexture(1280, 720, 24);
            cam.targetTexture = renderTexture;
            cam.Render();

            RenderTexture.active = renderTexture;
            var image = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
            image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(renderTexture);
            UnityEngine.Object.DestroyImmediate(image);
            Debug.Log($"[TestScene] Screenshot saved: {path}");
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
