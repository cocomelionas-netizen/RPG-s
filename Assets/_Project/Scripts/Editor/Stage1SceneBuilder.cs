using System;
using System.IO;
using System.Linq;
using RPG.CameraSystem;
using RPG.Controls;
using RPG.Player;
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
    /// Builds the Stage 1 test scene: 2D renderer setup, placeholder art and tiles,
    /// an isometric test map with obstacles, the player prefab and the follow camera.
    /// Menu: RPG > Stage 1 > Build Test Scene. Safe to run again (it regenerates everything).
    /// </summary>
    public static class Stage1SceneBuilder
    {
        public const string ScenePath = "Assets/_Project/Scenes/World_Test.unity";
        private const string TilesFolder = "Assets/_Project/Tiles";
        private const string PlayerPrefabPath = "Assets/_Project/Prefabs/Characters/Player.prefab";
        private const string Renderer2DPath = "Assets/_Project/Settings/Rendering/Renderer2D.asset";
        private static readonly string[] PipelineAssetPaths =
        {
            "Assets/Settings/PC_RPAsset.asset",
            "Assets/Settings/Mobile_RPAsset.asset",
        };

        private const int MapSize = 40;
        private const int Seed = 12345;

        [MenuItem("RPG/Stage 1/Build Test Scene")]
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
            Sprite playerSprite = PlaceholderArtGenerator.Character("Player_Placeholder", new Color(0.2f, 0.32f, 0.62f));

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Grid grid = CreateGrid();
            Tilemap ground = CreateTilemap(grid, "Ground", -100, TilemapRenderer.Mode.Chunk);
            Tilemap obstacles = CreateTilemap(grid, "Obstacles", 0, TilemapRenderer.Mode.Individual);
            obstacles.gameObject.AddComponent<TilemapCollider2D>();
            PaintTestMap(ground, obstacles, grassTiles, pathTile, rockTile);

            CreateGlobalLight();
            CreateEventSystem();

            GameObject playerPrefab = CreatePlayerPrefab(playerSprite);
            var player = (GameObject)PrefabUtility.InstantiatePrefab(playerPrefab);
            Vector3Int center = new Vector3Int(MapSize / 2, MapSize / 2, 0);
            player.transform.position = grid.GetCellCenterWorld(center);

            CreateCamera(player.transform);

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath);
            AddSceneToBuildSettings();
            EditorSettings.defaultBehaviorMode = EditorBehaviorMode.Mode2D;
            AssetDatabase.SaveAssets();

            Debug.Log($"[Stage1] Test scene built: {ScenePath}");
        }

        /// <summary>Batch mode entry: Unity.exe -batchmode -projectPath . -executeMethod RPG.EditorTools.Stage1SceneBuilder.BuildFromCommandLine -quit</summary>
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

        private static GameObject CreatePlayerPrefab(Sprite sprite)
        {
            var player = new GameObject("Player");

            var spriteRenderer = player.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
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

            Directory.CreateDirectory(Path.GetDirectoryName(PlayerPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(player, PlayerPrefabPath);
            UnityEngine.Object.DestroyImmediate(player);
            return prefab;
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
            Debug.Log($"[Stage1] Screenshot saved: {path}");
        }

        private static string GetArgument(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
