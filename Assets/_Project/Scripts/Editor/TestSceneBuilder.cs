using System;
using System.IO;
using System.Linq;
using RPG.CameraSystem;
using RPG.Characters;
using RPG.Combat;
using RPG.Controls;
using RPG.Monsters;
using RPG.Navigation;
using RPG.Networking;
using RPG.Player;
using RPG.Progression;
using RPG.UI;
using RPG.World;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

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
        private const string DummyPrefabPath = "Assets/_Project/Prefabs/World/TrainingDummy.prefab";
        private const string GoblinPrefabPath = "Assets/_Project/Prefabs/Monsters/Goblin.prefab";
        private const string NetworkPrefabsListPath = "Assets/DefaultNetworkPrefabs.asset";
        private const string PlayerAnimationSetPath = "Assets/_Project/Data/Characters/Player_AnimationSet.asset";
        private const string PlayerArtFolder = PlaceholderArtGenerator.Folder + "/Player";
        private const string GoblinAnimationSetPath = "Assets/_Project/Data/Characters/Goblin_AnimationSet.asset";
        private const string GoblinArtFolder = PlaceholderArtGenerator.Folder + "/Goblin";
        private const string RangerAnimationSetPath = "Assets/_Project/Data/Characters/Ranger_AnimationSet.asset";
        private const string RangerArtFolder = PlaceholderArtGenerator.Folder + "/Ranger";
        private const string MageAnimationSetPath = "Assets/_Project/Data/Characters/Mage_AnimationSet.asset";
        private const string MageArtFolder = PlaceholderArtGenerator.Folder + "/Mage";
        private const string ClassesFolder = "Assets/_Project/Data/Classes";
        private const string AbilitiesFolder = "Assets/_Project/Data/Abilities";
        // Visible radius of the GroundRing sprite at scale 1 (128 px wide, ring at 82 %, 256 px per unit).
        private const float GroundRingRadius = 0.205f;
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
            // Batch mode (CI builds) has no user to ask and nothing open worth saving.
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
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
            CharacterAnimationSet playerAnimations = CreateCharacterAnimationSet("Player", PlayerArtFolder, PlayerAnimationSetPath, new Color(0.2f, 0.32f, 0.62f), null);
            CharacterAnimationSet goblinAnimations = CreateCharacterAnimationSet("Goblin", GoblinArtFolder, GoblinAnimationSetPath,
                new Color(0.42f, 0.27f, 0.16f), new Color(0.45f, 0.62f, 0.27f));
            GameObject treePrefab = CreateTreePrefab(treeSprite);
            Sprite barSprite = PlaceholderArtGenerator.BarPixel("BarPixel");
            Sprite targetRingSprite = PlaceholderArtGenerator.GroundRing("TargetRing", new Color(0.95f, 0.2f, 0.15f, 0.9f));
            GameObject dummyPrefab = CreateDummyPrefab(PlaceholderArtGenerator.TrainingDummy("TrainingDummy"), barSprite);
            GameObject goblinPrefab = CreateGoblinPrefab(goblinAnimations, barSprite);
            CharacterAnimationSet rangerAnimations = CreateCharacterAnimationSet("Ranger", RangerArtFolder, RangerAnimationSetPath, new Color(0.2f, 0.45f, 0.22f), null);
            CharacterAnimationSet mageAnimations = CreateCharacterAnimationSet("Mage", MageArtFolder, MageAnimationSetPath, new Color(0.45f, 0.22f, 0.62f), null);
            ClassCatalog classes = CreateClasses(playerAnimations, rangerAnimations, mageAnimations);
            Sprite projectileSprite = PlaceholderArtGenerator.Orb("Projectile");
            Sprite effectRingSprite = PlaceholderArtGenerator.GroundRing("EffectRing", Color.white);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Grid grid = CreateGrid();
            Tilemap ground = CreateTilemap(grid, "Ground", -100, TilemapRenderer.Mode.Chunk);
            Tilemap obstacles = CreateTilemap(grid, "Obstacles", 0, TilemapRenderer.Mode.Individual);
            obstacles.gameObject.AddComponent<TilemapCollider2D>();
            PaintTestMap(ground, obstacles, grassTiles, pathTile, rockTile);
            PlaceTrees(grid, obstacles, treePrefab);
            grid.gameObject.AddComponent<WorldNavigation>().Configure(grid, ground, obstacles);

            CreateGlobalLight();
            CreateEventSystem();

            // Players are not placed in the scene: the server spawns one per connection at the spawn point.
            GameObject playerPrefab = CreatePlayerPrefab(playerAnimations, barSprite, classes);
            var spawnPoint = new GameObject("Player Spawn").transform;
            spawnPoint.position = grid.GetCellCenterWorld(new Vector3Int(MapSize / 2, MapSize / 2, 0));

            CreateNetworkManager(playerPrefab, spawnPoint);
            CreateCamera(spawnPoint);
            CreateClickMarker(markerSprite, null);
            CreateTargetIndicator(targetRingSprite);
            new GameObject("Combat Effects").AddComponent<CombatEffects>().Configure(projectileSprite, effectRingSprite, GroundRingRadius);
            PlaceDummies(grid, dummyPrefab);
            PlaceMonsterCamps(grid, goblinPrefab);
            CreateHud();

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

        private static GameObject CreatePlayerPrefab(CharacterAnimationSet animations, Sprite barSprite, ClassCatalog classes)
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
            player.AddComponent<NetworkObject>();
            player.AddComponent<PlayerNetworkMovement>();

            AddHealth(player, "Player", true, Faction.Player, 100);
            player.AddComponent<HealthRegeneration>();
            player.AddComponent<AutoAttack>();
            player.AddComponent<PlayerTargeting>();
            player.AddComponent<PlayerRespawn>();
            var progress = player.AddComponent<CharacterProgress>();
            var progressObject = new SerializedObject(progress);
            progressObject.FindProperty("catalog").objectReferenceValue = classes;
            progressObject.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<AbilityCaster>();
            AddHealthPresenter(player, barSprite, 0.85f);
            AddHitbox(player, new Vector2(0.36f, 0.62f), new Vector2(0f, 0.34f));

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

        private static CharacterAnimationSet CreateCharacterAnimationSet(string prefix, string artFolder, string assetPath, Color cloakColor, Color? skinColor)
        {
            float[] idleBob = { 0f, 1.5f };
            float[] walkBob = { 0f, 3f, 0f, 3f };
            float[] walkStride = { 1f, 0f, -1f, 0f };

            var idle = new DirectionalClip { id = CharacterAnimationSet.IdleClip, framesPerSecond = 2f, loop = true };
            var walk = new DirectionalClip { id = CharacterAnimationSet.WalkClip, framesPerSecond = 8f, loop = true };
            var attack = new DirectionalClip { id = AutoAttack.AttackClip, framesPerSecond = 10f, loop = false };
            float[] attackSwing = { -70f, -10f, 50f };

            foreach ((Direction8 direction, Vector2 facing) in DrawnDirections)
            {
                var idleFrames = new Sprite[idleBob.Length];
                for (int i = 0; i < idleFrames.Length; i++)
                    idleFrames[i] = PlaceholderArtGenerator.CharacterFrame(artFolder, $"{prefix}_Idle_{direction}_{i}", cloakColor, facing, idleBob[i], 0f, null, skinColor);

                var walkFrames = new Sprite[walkBob.Length];
                for (int i = 0; i < walkFrames.Length; i++)
                    walkFrames[i] = PlaceholderArtGenerator.CharacterFrame(artFolder, $"{prefix}_Walk_{direction}_{i}", cloakColor, facing, walkBob[i], walkStride[i], null, skinColor);

                var attackFrames = new Sprite[attackSwing.Length];
                for (int i = 0; i < attackFrames.Length; i++)
                    attackFrames[i] = PlaceholderArtGenerator.CharacterFrame(artFolder, $"{prefix}_Attack_{direction}_{i}", cloakColor, facing, 1f, 0f, attackSwing[i], skinColor);

                SetFrames(idle, direction, idleFrames);
                SetFrames(walk, direction, walkFrames);
                SetFrames(attack, direction, attackFrames);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(assetPath));
            var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(assetPath);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
                AssetDatabase.CreateAsset(set, assetPath);
            }
            set.MirrorWestFromEast = true;
            set.Clips = new[] { idle, walk, attack };
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

        private static void CreateNetworkManager(GameObject playerPrefab, Transform spawnPoint)
        {
            var go = new GameObject("NetworkManager");
            var network = go.AddComponent<NetworkManager>();
            var transport = go.AddComponent<UnityTransport>();
            network.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = transport,
                PlayerPrefab = playerPrefab,
                ConnectionApproval = true,
            };
            var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (prefabsList != null)
                network.NetworkConfig.Prefabs.NetworkPrefabsLists.Add(prefabsList);

            var bootstrap = go.AddComponent<NetworkBootstrap>();
            var bootstrapObject = new SerializedObject(bootstrap);
            bootstrapObject.FindProperty("spawnPoint").objectReferenceValue = spawnPoint;
            bootstrapObject.ApplyModifiedPropertiesWithoutUndo();

            go.AddComponent<DevConnectionPanel>();
            go.AddComponent<DevBot>();
        }

        private static Health AddHealth(GameObject go, string unitName, bool appendOwnerId, Faction faction, int maxHealth)
        {
            var health = go.AddComponent<Health>();
            var healthObject = new SerializedObject(health);
            healthObject.FindProperty("defaultName").stringValue = unitName;
            healthObject.FindProperty("appendOwnerId").boolValue = appendOwnerId;
            healthObject.FindProperty("faction").enumValueIndex = (int)faction;
            healthObject.FindProperty("maxHealth").intValue = maxHealth;
            healthObject.ApplyModifiedPropertiesWithoutUndo();
            return health;
        }

        private static void AddHealthPresenter(GameObject go, Sprite barSprite, float barHeight)
        {
            var presenter = go.AddComponent<HealthPresenter>();
            var presenterObject = new SerializedObject(presenter);
            presenterObject.FindProperty("body").objectReferenceValue = go.GetComponent<SpriteRenderer>();
            presenterObject.FindProperty("barSprite").objectReferenceValue = barSprite;
            presenterObject.FindProperty("barHeightAboveFeet").floatValue = barHeight;
            presenterObject.FindProperty("damageNumberHeight").floatValue = barHeight + 0.05f;
            presenterObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>A trigger around the body for mouse selection; it does not block movement.</summary>
        private static void AddHitbox(GameObject go, Vector2 size, Vector2 offset)
        {
            var hitbox = new GameObject("Hitbox");
            hitbox.transform.SetParent(go.transform, false);
            var box = hitbox.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = size;
            box.offset = offset;
        }

        private static GameObject CreateDummyPrefab(Sprite sprite, Sprite barSprite)
        {
            var dummy = new GameObject("Training Dummy");
            var spriteRenderer = dummy.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

            var post = dummy.AddComponent<CircleCollider2D>();
            post.radius = 0.08f;
            post.offset = new Vector2(0f, 0.03f);

            dummy.AddComponent<NetworkObject>();
            AddHealth(dummy, "Training Dummy", false, Faction.Monster, 200);
            dummy.AddComponent<TrainingDummy>();
            AddHealthPresenter(dummy, barSprite, 1.2f);
            AddHitbox(dummy, new Vector2(0.4f, 0.95f), new Vector2(0f, 0.5f));

            Directory.CreateDirectory(Path.GetDirectoryName(DummyPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(dummy, DummyPrefabPath);
            UnityEngine.Object.DestroyImmediate(dummy);
            return prefab;
        }

        private static ClassCatalog CreateClasses(CharacterAnimationSet warriorLook, CharacterAnimationSet rangerLook, CharacterAnimationSet mageLook)
        {
            var steel = new Color(0.85f, 0.85f, 0.95f);
            var green = new Color(0.45f, 0.95f, 0.4f);
            var fire = new Color(1f, 0.5f, 0.15f);
            var frost = new Color(0.5f, 0.85f, 1f);
            var heal = new Color(0.4f, 1f, 0.5f);

            CharacterClassDefinition warrior = CreateClass("Warrior", warriorLook, 110, 11, 8, 12, 1f, 1.5f, 0.9f, 0.1f, Color.clear, new[]
            {
                CreateAbility("Warrior_HeavyStrike", "Heavy Strike", AbilityKind.TargetDamage, 6f, 1f, 0f, 2.2f, 0f, steel),
                CreateAbility("Warrior_Whirlwind", "Whirlwind", AbilityKind.SelfArea, 10f, 0f, 1.4f, 1.3f, 0f, steel),
                CreateAbility("Warrior_SecondWind", "Second Wind", AbilityKind.SelfHeal, 30f, 0f, 0f, 0f, 0.3f, heal),
            });
            CharacterClassDefinition ranger = CreateClass("Ranger", rangerLook, 90, 9, 6, 9, 0.8f, 1.7f, 5f, 0.15f, new Color(0.85f, 0.75f, 0.45f), new[]
            {
                CreateAbility("Ranger_AimedShot", "Aimed Shot", AbilityKind.TargetDamage, 7f, 6f, 0f, 2.2f, 0f, green),
                CreateAbility("Ranger_Volley", "Volley", AbilityKind.TargetArea, 10f, 6f, 1.3f, 1.2f, 0f, green),
                CreateAbility("Ranger_Mend", "Mend", AbilityKind.SelfHeal, 30f, 0f, 0f, 0f, 0.25f, heal),
            });
            CharacterClassDefinition mage = CreateClass("Mage", mageLook, 80, 8, 5, 8, 0.8f, 1.8f, 4.5f, 0.1f, new Color(0.7f, 0.5f, 1f), new[]
            {
                CreateAbility("Mage_Fireball", "Fireball", AbilityKind.TargetDamage, 5f, 5.5f, 0f, 2.8f, 0f, fire),
                CreateAbility("Mage_Flamestrike", "Flamestrike", AbilityKind.TargetArea, 12f, 5.5f, 1.4f, 1.8f, 0f, fire),
                CreateAbility("Mage_FrostNova", "Frost Nova", AbilityKind.SelfArea, 14f, 0f, 1.8f, 1.4f, 0f, frost),
            });

            var catalog = LoadOrCreateAsset<ClassCatalog>($"{ClassesFolder}/ClassCatalog.asset");
            // Players store the index, so keep this order.
            catalog.classes = new[] { warrior, ranger, mage };
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static CharacterClassDefinition CreateClass(string className, CharacterAnimationSet look, int baseHealth, int healthPerLevel,
            int minDamage, int maxDamage, float damagePerLevel, float attackInterval, float attackRange, float critChance,
            Color projectileColor, AbilityDefinition[] abilities)
        {
            var definition = LoadOrCreateAsset<CharacterClassDefinition>($"{ClassesFolder}/{className}.asset");
            definition.displayName = className;
            definition.animations = look;
            definition.baseHealth = baseHealth;
            definition.healthPerLevel = healthPerLevel;
            definition.minDamage = minDamage;
            definition.maxDamage = maxDamage;
            definition.damagePerLevel = damagePerLevel;
            definition.attackInterval = attackInterval;
            definition.attackRange = attackRange;
            definition.critChance = critChance;
            definition.projectileColor = projectileColor;
            definition.abilities = abilities;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static AbilityDefinition CreateAbility(string fileName, string displayName, AbilityKind kind, float cooldown,
            float range, float radius, float damageMultiplier, float healFraction, Color effectColor)
        {
            var ability = LoadOrCreateAsset<AbilityDefinition>($"{AbilitiesFolder}/{fileName}.asset");
            ability.displayName = displayName;
            ability.kind = kind;
            ability.cooldown = cooldown;
            ability.range = range;
            ability.radius = radius;
            ability.damageMultiplier = damageMultiplier;
            ability.healFraction = healFraction;
            ability.effectColor = effectColor;
            EditorUtility.SetDirty(ability);
            return ability;
        }

        private static T LoadOrCreateAsset<T>(string path) where T : ScriptableObject
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }
            return asset;
        }

        private static GameObject CreateGoblinPrefab(CharacterAnimationSet animations, Sprite barSprite)
        {
            var goblin = new GameObject("Goblin");
            // Smaller than the player.
            goblin.transform.localScale = new Vector3(0.85f, 0.85f, 1f);

            var spriteRenderer = goblin.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = animations.FindClip(CharacterAnimationSet.IdleClip).south[0];
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

            // Moved by the server, not by physics; no solid collider, so players and monsters pass through each other.
            var body = goblin.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;

            goblin.AddComponent<NetworkObject>();
            goblin.AddComponent<MonsterMotion>();
            goblin.AddComponent<CharacterAnimator>().AnimationSet = animations;

            AddHealth(goblin, "Goblin", false, Faction.Monster, 45);
            var autoAttack = goblin.AddComponent<AutoAttack>();
            var attackObject = new SerializedObject(autoAttack);
            attackObject.FindProperty("minDamage").intValue = 3;
            attackObject.FindProperty("maxDamage").intValue = 5;
            attackObject.FindProperty("attackInterval").floatValue = 2f;
            attackObject.FindProperty("range").floatValue = 0.7f;
            attackObject.FindProperty("critChance").floatValue = 0.05f;
            attackObject.ApplyModifiedPropertiesWithoutUndo();

            var ai = goblin.AddComponent<MonsterAI>();
            var aiObject = new SerializedObject(ai);
            aiObject.FindProperty("level").intValue = 2;
            aiObject.FindProperty("experienceReward").intValue = 20;
            aiObject.ApplyModifiedPropertiesWithoutUndo();
            AddHealthPresenter(goblin, barSprite, 0.85f);
            AddHitbox(goblin, new Vector2(0.4f, 0.65f), new Vector2(0f, 0.34f));

            Directory.CreateDirectory(Path.GetDirectoryName(GoblinPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(goblin, GoblinPrefabPath);
            UnityEngine.Object.DestroyImmediate(goblin);
            RegisterNetworkPrefab(prefab);
            return prefab;
        }

        /// <summary>Prefabs spawned at runtime must be in the network prefab list on server and clients.</summary>
        private static void RegisterNetworkPrefab(GameObject prefab)
        {
            var prefabsList = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(NetworkPrefabsListPath);
            if (prefabsList == null)
            {
                Debug.LogWarning($"[TestScene] {NetworkPrefabsListPath} not found; {prefab.name} is not registered as a network prefab.");
                return;
            }
            if (prefabsList.Contains(prefab)) return;

            prefabsList.Add(new NetworkPrefab { Prefab = prefab });
            EditorUtility.SetDirty(prefabsList);
        }

        private static void PlaceMonsterCamps(Grid grid, GameObject monsterPrefab)
        {
            int center = MapSize / 2;
            // Away from the spawn so new players are not attacked right away (5-7 units from it).
            var camps = new[]
            {
                new Vector3Int(center + 12, center + 6, 0),
                new Vector3Int(center - 10, center + 4, 0),
                new Vector3Int(center + 4, center - 10, 0),
            };

            var parent = new GameObject("Monster Camps").transform;
            for (int i = 0; i < camps.Length; i++)
            {
                var camp = new GameObject($"Goblin Camp {i + 1}");
                camp.transform.SetParent(parent, false);
                camp.transform.position = grid.GetCellCenterWorld(camps[i]);
                camp.AddComponent<MonsterSpawner>().Configure(monsterPrefab, 3, 1.2f, 20f);
            }
        }

        private static void PlaceDummies(Grid grid, GameObject dummyPrefab)
        {
            int center = MapSize / 2;
            // Right and left of the spawn, on grass inside the cleared spawn area.
            var cells = new[] { new Vector3Int(center + 2, center - 2, 0), new Vector3Int(center - 3, center - 1, 0) };
            var parent = new GameObject("Training Dummies").transform;
            foreach (Vector3Int cell in cells)
            {
                var dummy = (GameObject)PrefabUtility.InstantiatePrefab(dummyPrefab, parent);
                dummy.transform.position = grid.GetCellCenterWorld(cell);
            }
        }

        private static void CreateTargetIndicator(Sprite ringSprite)
        {
            var go = new GameObject("Target Indicator");
            var ring = go.AddComponent<SpriteRenderer>();
            ring.sprite = ringSprite;
            ring.sortingOrder = -49;
            ring.enabled = false;
            var indicator = go.AddComponent<TargetIndicator>();
            var indicatorObject = new SerializedObject(indicator);
            indicatorObject.FindProperty("ring").objectReferenceValue = ring;
            indicatorObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void CreateHud()
        {
            var canvasObject = new GameObject("HUD", typeof(RectTransform));
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObject.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            var combatText = new GameObject("Combat Text", typeof(RectTransform));
            combatText.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)combatText.transform);
            combatText.AddComponent<CombatTextLayer>();

            CreateUnitFrame(canvasObject.transform, "Player Frame", UnitFrameRole.LocalPlayer, new Vector2(0.5f, 0f), new Vector2(0f, 40f));
            CreateUnitFrame(canvasObject.transform, "Target Frame", UnitFrameRole.Target, new Vector2(0.5f, 1f), new Vector2(0f, -30f));

            // These build their own contents at runtime.
            CreateHudElement<ExperienceBar>(canvasObject.transform, "Experience Bar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(380f, 16f));
            CreateHudElement<AbilityBar>(canvasObject.transform, "Ability Bar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 122f), new Vector2(400f, 80f));
            CreateHudElement<ClassPicker>(canvasObject.transform, "Class Picker", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -60f), new Vector2(130f, 140f));
        }

        private static void CreateHudElement<T>(Transform canvas, string elementName, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size) where T : Component
        {
            var go = new GameObject(elementName, typeof(RectTransform));
            go.transform.SetParent(canvas, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            go.AddComponent<T>();
        }

        private static void CreateUnitFrame(Transform canvas, string frameName, UnitFrameRole role, Vector2 anchor, Vector2 offset)
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var holder = new GameObject(frameName, typeof(RectTransform));
            holder.transform.SetParent(canvas, false);
            var holderRect = (RectTransform)holder.transform;
            holderRect.anchorMin = holderRect.anchorMax = holderRect.pivot = anchor;
            holderRect.anchoredPosition = offset;
            holderRect.sizeDelta = new Vector2(380f, 70f);

            var panel = CreateImage("Panel", holder.transform, new Color(0.08f, 0.07f, 0.06f, 0.85f));
            Stretch(panel.rectTransform);

            Text nameText = CreateText("Name", panel.transform, font, 22, TextAnchor.UpperLeft);
            nameText.rectTransform.anchorMin = new Vector2(0f, 0.5f);
            nameText.rectTransform.anchorMax = new Vector2(1f, 1f);
            nameText.rectTransform.offsetMin = new Vector2(12f, 0f);
            nameText.rectTransform.offsetMax = new Vector2(-12f, -6f);

            var barBackground = CreateImage("Bar", panel.transform, new Color(0f, 0f, 0f, 0.8f));
            barBackground.rectTransform.anchorMin = new Vector2(0f, 0f);
            barBackground.rectTransform.anchorMax = new Vector2(1f, 0.5f);
            barBackground.rectTransform.offsetMin = new Vector2(12f, 10f);
            barBackground.rectTransform.offsetMax = new Vector2(-12f, -2f);

            var fill = CreateImage("Fill", barBackground.transform, Color.red);
            Stretch(fill.rectTransform);

            Text valueText = CreateText("Value", barBackground.transform, font, 16, TextAnchor.MiddleCenter);
            Stretch(valueText.rectTransform);

            var frame = holder.AddComponent<UnitFrame>();
            var frameObject = new SerializedObject(frame);
            frameObject.FindProperty("role").enumValueIndex = (int)role;
            frameObject.FindProperty("panel").objectReferenceValue = panel.gameObject;
            frameObject.FindProperty("nameText").objectReferenceValue = nameText;
            frameObject.FindProperty("fill").objectReferenceValue = fill.rectTransform;
            frameObject.FindProperty("fillImage").objectReferenceValue = fill;
            frameObject.FindProperty("valueText").objectReferenceValue = valueText;
            frameObject.ApplyModifiedPropertiesWithoutUndo();

            panel.gameObject.SetActive(false);
        }

        private static UnityEngine.UI.Image CreateImage(string imageName, Transform parent, Color color)
        {
            var go = new GameObject(imageName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<UnityEngine.UI.Image>();
            image.color = color;
            return image;
        }

        private static Text CreateText(string textName, Transform parent, Font font, int size, TextAnchor alignment)
        {
            var go = new GameObject(textName, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            go.AddComponent<UnityEngine.UI.Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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
