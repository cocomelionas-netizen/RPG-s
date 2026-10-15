using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RPG.CameraSystem;
using RPG.Accounts;
using RPG.Characters;
using RPG.Combat;
using RPG.Controls;
using RPG.Dungeons;
using RPG.Items;
using RPG.Monsters;
using RPG.Navigation;
using RPG.Networking;
using RPG.Player;
using RPG.Progression;
using RPG.Vendors;
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
        private const string MonstersFolder = "Assets/_Project/Prefabs/Monsters";
        private const string LootBagPrefabPath = "Assets/_Project/Prefabs/World/LootBag.prefab";
        private const string ChestPrefabFolder = "Assets/_Project/Prefabs/World";
        private const string ItemsFolder = "Assets/_Project/Data/Items";
        private const string AffixesFolder = "Assets/_Project/Data/Items/Affixes";
        private const string DungeonInstancePrefabPath = "Assets/_Project/Prefabs/World/DungeonInstance.prefab";
        private const string DungeonDefinitionPath = "Assets/_Project/Data/Dungeons/GoblinDen.asset";
        private const string KingAnimationSetPath = "Assets/_Project/Data/Characters/GoblinKing_AnimationSet.asset";
        private const string KingArtFolder = PlaceholderArtGenerator.Folder + "/GoblinKing";
        private const string VendorsFolder = "Assets/_Project/Data/Vendors";
        private const string VendorArtFolder = PlaceholderArtGenerator.Folder + "/Vendors";
        private const int WarriorClass = 0;
        private const int RangerClass = 1;
        private const int MageClass = 2;
        private const string BruteAnimationSetPath = "Assets/_Project/Data/Characters/GoblinBrute_AnimationSet.asset";
        private const string BruteArtFolder = PlaceholderArtGenerator.Folder + "/GoblinBrute";
        private const string OrcAnimationSetPath = "Assets/_Project/Data/Characters/Orc_AnimationSet.asset";
        private const string OrcArtFolder = PlaceholderArtGenerator.Folder + "/Orc";
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

        // The world is the size of Source's start region, laid out from its data (SourceStartArea).
        private const int MapWidth = SourceStartArea.Width;
        private const int MapHeight = SourceStartArea.Height;
        // New characters start in Pilgrim's Refuge; the main town with the vendors is St. Revere, like in Source.
        private static Vector3Int StartCell => SourceStartArea.Start;
        private static Vector3Int TownCell => SourceStartArea.Town;
        // Paved squares, and areas without rocks or trees, around both.
        private const int TownSquareRadius = 6;
        private const int TownClearRadius = 18;
        private const int StartSquareRadius = 3;
        private const int StartClearRadius = 6;
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
            CharacterAnimationSet bruteAnimations = CreateCharacterAnimationSet("GoblinBrute", BruteArtFolder, BruteAnimationSetPath,
                new Color(0.33f, 0.31f, 0.29f), new Color(0.3f, 0.45f, 0.2f));
            CharacterAnimationSet orcAnimations = CreateCharacterAnimationSet("Orc", OrcArtFolder, OrcAnimationSetPath,
                new Color(0.45f, 0.12f, 0.1f), new Color(0.42f, 0.52f, 0.36f));
            ItemCatalog items = CreateItems(out ItemDefinition potion, out ItemDefinition manaPotion);
            AffixCatalog affixes = CreateAffixes();
            GameObject lootBagPrefab = CreateLootBagPrefab(PlaceholderArtGenerator.LootSack("LootSack"));
            GameObject goblinPrefab = CreateMonsterPrefab(new MonsterSpec
            {
                Name = "Goblin", Animations = goblinAnimations, Scale = 0.85f, Health = 45, MinDamage = 3, MaxDamage = 5,
                AttackInterval = 2f, Level = 2, Experience = LevelCurve.KillExperienceFor(2), MoveSpeed = 1.8f, Gold = new Vector2Int(3, 6),
                Loot = Loot(items, ("GoblinEar", 0.6f), ("DenKey", 0.04f), ("ChippedRuby", 0.02f), ("ChippedTopaz", 0.02f), ("ChippedSapphire", 0.02f), ("ChippedEmerald", 0.02f), ("HealingPotion", 0.3f), ("ManaPotion", 0.15f)),
            }, barSprite, lootBagPrefab, affixes);
            GameObject brutePrefab = CreateMonsterPrefab(new MonsterSpec
            {
                Name = "Goblin Brute", Animations = bruteAnimations, Scale = 1.05f, Health = 120, MinDamage = 6, MaxDamage = 10,
                AttackInterval = 2.2f, Level = 4, Experience = LevelCurve.KillExperienceFor(4), MoveSpeed = 1.6f, Gold = new Vector2Int(10, 16),
                Loot = Loot(items, ("BruteTusk", 0.6f), ("DenKey", 0.25f), ("ChippedRuby", 0.04f), ("ChippedTopaz", 0.04f), ("ChippedSapphire", 0.04f), ("ChippedEmerald", 0.04f), ("HealingPotion", 0.5f), ("ManaPotion", 0.25f)),
            }, barSprite, lootBagPrefab, affixes);
            GameObject orcPrefab = CreateMonsterPrefab(new MonsterSpec
            {
                Name = "Orc", Animations = orcAnimations, Scale = 1.15f, Health = 220, MinDamage = 10, MaxDamage = 15,
                AttackInterval = 2f, Level = 6, Experience = LevelCurve.KillExperienceFor(6), MoveSpeed = 1.7f, Gold = new Vector2Int(20, 30),
                Loot = Loot(items, ("OrcFang", 0.6f), ("DenKey", 0.35f), ("ChippedRuby", 0.05f), ("ChippedTopaz", 0.05f), ("ChippedSapphire", 0.05f), ("ChippedEmerald", 0.05f), ("Ruby", 0.01f), ("Sapphire", 0.01f), ("HealingPotion", 0.6f), ("ManaPotion", 0.3f)),
            }, barSprite, lootBagPrefab, affixes);
            CharacterAnimationSet kingAnimations = CreateCharacterAnimationSet("GoblinKing", KingArtFolder, KingAnimationSetPath,
                new Color(0.75f, 0.58f, 0.15f), new Color(0.4f, 0.58f, 0.25f));
            GameObject kingPrefab = CreateMonsterPrefab(new MonsterSpec
            {
                Name = "Goblin King", Animations = kingAnimations, Scale = 1.5f, Health = 900, MinDamage = 14, MaxDamage = 20,
                AttackInterval = 2.2f, Level = 8, Experience = LevelCurve.KillExperienceFor(8) * 4, MoveSpeed = 1.5f, Gold = new Vector2Int(150, 200),
                Loot = Loot(items, ("CrownShard", 1f), ("Ruby", 0.25f), ("Topaz", 0.25f), ("Sapphire", 0.25f), ("Emerald", 0.25f), ("FlawlessRuby", 0.05f), ("FlawlessEmerald", 0.05f), ("HealingPotion", 1f), ("ManaPotion", 1f)),
            }, barSprite, lootBagPrefab, affixes, boss =>
            {
                boss.AddComponent<BossMechanics>();
                // Like most Source bosses: one gold piece for sure, a purple one a quarter of the time.
                boss.GetComponent<MonsterAI>().ConfigureGuaranteedLoot(1, ItemRarity.Epic, 0.25f);
            });
            DungeonDefinition dungeon = CreateDungeon(brutePrefab, orcPrefab, kingPrefab);
            ChestPrefabs chests = CreateChestPrefabs(items, affixes, lootBagPrefab);
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
            var navigation = grid.gameObject.AddComponent<WorldNavigation>();
            navigation.Configure(grid, ground, obstacles);
            navigation.ZoneName = "Fanadin";

            CreateGlobalLight();
            CreateEventSystem();

            // Players are not placed in the scene: the server spawns one per connection at the spawn point.
            GameObject playerPrefab = CreatePlayerPrefab(playerAnimations, barSprite, classes, items, affixes, potion, manaPotion);
            var spawnPoint = new GameObject("Player Spawn").transform;
            spawnPoint.position = grid.GetCellCenterWorld(StartCell);

            CreateNetworkManager(playerPrefab, spawnPoint);
            CreateCamera(spawnPoint);
            CreateClickMarker(markerSprite, null);
            CreateTargetIndicator(targetRingSprite);
            new GameObject("Combat Effects").AddComponent<CombatEffects>().Configure(projectileSprite, effectRingSprite, GroundRingRadius);
            PlaceDummies(grid, dummyPrefab);
            PlaceMonsters(grid, obstacles, goblinPrefab, brutePrefab, orcPrefab);
            PlaceChests(grid, obstacles, chests);
            PlaceDungeon(grid, obstacles, dungeon);
            PlaceVendors(grid, CreateVendors(items, potion, manaPotion));
            PlaceZonesAndWaystones(grid, obstacles, PlaceholderArtGenerator.Waystone("Waystone"));
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
            Vector3Int town = TownCell;
            Vector3Int start = StartCell;

            for (int x = 0; x < MapWidth; x++)
            for (int y = 0; y < MapHeight; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                // Roads cross at the start and in town.
                bool isPath = x == town.x || y == town.y || x == start.x || y == start.y;
                bool square = Within(cell, town, TownSquareRadius) || Within(cell, start, StartSquareRadius);
                ground.SetTile(cell, isPath || square ? pathTile : grassTiles[random.Next(grassTiles.Length)]);

                bool isBorder = x == 0 || y == 0 || x == MapWidth - 1 || y == MapHeight - 1;
                bool scatteredRock = !isPath && !IsCleared(cell, 0) && random.NextDouble() < 0.05;
                if (isBorder || scatteredRock)
                    obstacles.SetTile(cell, rockTile);
            }

            // A small wall near the start to test collisions and sorting (walk behind / in front of it).
            for (int i = 0; i < 4; i++)
                obstacles.SetTile(new Vector3Int(start.x + 3 + i, start.y + 2, 0), rockTile);
        }

        private static bool Within(Vector3Int cell, Vector3Int center, int radius) =>
            Mathf.Abs(cell.x - center.x) <= radius && Mathf.Abs(cell.y - center.y) <= radius;

        /// <summary>Inside the cleared town or start area (no rocks, trees, or monsters' spots), shrunk by <paramref name="shrink"/>.</summary>
        private static bool IsCleared(Vector3Int cell, int shrink) =>
            Within(cell, TownCell, TownClearRadius - shrink) || Within(cell, StartCell, StartClearRadius - shrink);

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

        private static GameObject CreatePlayerPrefab(CharacterAnimationSet animations, Sprite barSprite, ClassCatalog classes,
            ItemCatalog items, AffixCatalog affixes, ItemDefinition startingPotion, ItemDefinition startingManaPotion)
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
            player.AddComponent<Mana>();
            player.AddComponent<AutoAttack>();
            player.AddComponent<PlayerTargeting>();
            player.AddComponent<PlayerRespawn>();
            var progress = player.AddComponent<CharacterProgress>();
            var progressObject = new SerializedObject(progress);
            progressObject.FindProperty("catalog").objectReferenceValue = classes;
            progressObject.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<AbilityCaster>();
            var inventory = player.AddComponent<Inventory>();
            var inventoryObject = new SerializedObject(inventory);
            inventoryObject.FindProperty("catalog").objectReferenceValue = items;
            inventoryObject.FindProperty("affixes").objectReferenceValue = affixes;
            SerializedProperty startingItems = inventoryObject.FindProperty("startingItems");
            startingItems.arraySize = 5;
            for (int i = 0; i < startingItems.arraySize; i++)
                startingItems.GetArrayElementAtIndex(i).objectReferenceValue = i < 3 ? startingPotion : startingManaPotion;
            inventoryObject.ApplyModifiedPropertiesWithoutUndo();
            player.AddComponent<LootPicker>();
            player.AddComponent<CharacterPersistence>();
            player.AddComponent<DungeonTraveller>();
            player.AddComponent<WaystoneTraveller>();
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

        // Cells with a tree trunk, so chests are not put inside one.
        private static readonly HashSet<Vector3Int> TreeCells = new HashSet<Vector3Int>();

        /// <summary>The free cell (no rock, no tree) nearest to <paramref name="cell"/>, searching a few cells around it.</summary>
        private static Vector3Int OpenCellNear(Tilemap obstacles, Vector3Int cell)
        {
            for (int radius = 0; radius <= 4; radius++)
            for (int dx = -radius; dx <= radius; dx++)
            for (int dy = -radius; dy <= radius; dy++)
            {
                if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != radius) continue;
                var candidate = new Vector3Int(cell.x + dx, cell.y + dy, 0);
                if (!obstacles.HasTile(candidate) && !TreeCells.Contains(candidate)) return candidate;
            }
            return cell;
        }

        private static void PlaceTrees(Grid grid, Tilemap obstacles, GameObject treePrefab)
        {
            var random = new System.Random(TreeSeed);
            var props = new GameObject("Props").transform;
            TreeCells.Clear();
            Vector3Int town = TownCell;
            Vector3Int start = StartCell;

            for (int x = 2; x < MapWidth - 2; x++)
            for (int y = 2; y < MapHeight - 2; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                bool isPath = x == town.x || y == town.y || x == start.x || y == start.y;
                // A little sparser than before: the world is much bigger now.
                if (isPath || IsCleared(cell, 1) || obstacles.HasTile(cell) || random.NextDouble() >= 0.02)
                    continue;

                var tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, props);
                tree.transform.position = grid.GetCellCenterWorld(cell);
                TreeCells.Add(cell);
            }

            // One tree right next to the start to test walking behind and in front of tall objects.
            var nearTree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, props);
            nearTree.transform.position = grid.GetCellCenterWorld(new Vector3Int(start.x - 2, start.y + 1, 0));
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

            // Before the bootstrap, which looks for it when it wakes up.
            go.AddComponent<AccountService>();
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

            // Base health is lowered by what the level-1 attributes add, so totals stay as before attributes.
            CharacterClassDefinition warrior = CreateClass("Warrior", warriorLook, 35, 11, 5, 9, 1f, 1.5f, 0.9f, 0.06f, Color.clear,
                new AttributeSet(15, 10, 5, 15), RPG.Progression.Attribute.Strength, 20, 4, new[]
            {
                CreateAbility("Warrior_HeavyStrike", "Heavy Strike", AbilityKind.TargetDamage, 6f, 10, 1f, 0f, 2.2f, 0f, steel),
                CreateAbility("Warrior_Whirlwind", "Whirlwind", AbilityKind.SelfArea, 10f, 15, 0f, 1.4f, 1.3f, 0f, steel),
                CreateAbility("Warrior_SecondWind", "Second Wind", AbilityKind.SelfHeal, 30f, 20, 0f, 0f, 0f, 0.3f, heal),
            });
            CharacterClassDefinition ranger = CreateClass("Ranger", rangerLook, 40, 9, 3, 6, 0.8f, 1.7f, 5f, 0.09f, new Color(0.85f, 0.75f, 0.45f),
                new AttributeSet(10, 15, 10, 10), RPG.Progression.Attribute.Agility, 20, 6, new[]
            {
                CreateAbility("Ranger_AimedShot", "Aimed Shot", AbilityKind.TargetDamage, 7f, 12, 6f, 0f, 2.2f, 0f, green),
                CreateAbility("Ranger_Volley", "Volley", AbilityKind.TargetArea, 10f, 18, 6f, 1.3f, 1.2f, 0f, green),
                CreateAbility("Ranger_Mend", "Mend", AbilityKind.SelfHeal, 30f, 20, 0f, 0f, 0f, 0.25f, heal),
            });
            CharacterClassDefinition mage = CreateClass("Mage", mageLook, 35, 8, 0, 3, 0.8f, 1.8f, 4.5f, 0.08f, new Color(0.7f, 0.5f, 1f),
                new AttributeSet(5, 5, 20, 10), RPG.Progression.Attribute.Intelligence, 20, 10, new[]
            {
                CreateAbility("Mage_Fireball", "Fireball", AbilityKind.TargetDamage, 5f, 15, 5.5f, 0f, 2.8f, 0f, fire),
                CreateAbility("Mage_Flamestrike", "Flamestrike", AbilityKind.TargetArea, 12f, 25, 5.5f, 1.4f, 1.8f, 0f, fire),
                CreateAbility("Mage_FrostNova", "Frost Nova", AbilityKind.SelfArea, 14f, 30, 0f, 1.8f, 1.4f, 0f, frost),
            });

            var catalog = LoadOrCreateAsset<ClassCatalog>($"{ClassesFolder}/ClassCatalog.asset");
            // Players store the index, so keep this order.
            catalog.classes = new[] { warrior, ranger, mage };
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static CharacterClassDefinition CreateClass(string className, CharacterAnimationSet look, int baseHealth, int healthPerLevel,
            int minDamage, int maxDamage, float damagePerLevel, float attackInterval, float attackRange, float critChance,
            Color projectileColor, AttributeSet attributes, RPG.Progression.Attribute primaryAttribute, int baseMana, int manaPerLevel,
            AbilityDefinition[] abilities)
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
            definition.baseAttributes = attributes;
            definition.primaryAttribute = primaryAttribute;
            definition.baseMana = baseMana;
            definition.manaPerLevel = manaPerLevel;
            definition.abilities = abilities;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static AbilityDefinition CreateAbility(string fileName, string displayName, AbilityKind kind, float cooldown, int manaCost,
            float range, float radius, float damageMultiplier, float healFraction, Color effectColor)
        {
            var ability = LoadOrCreateAsset<AbilityDefinition>($"{AbilitiesFolder}/{fileName}.asset");
            ability.displayName = displayName;
            ability.kind = kind;
            ability.cooldown = cooldown;
            ability.manaCost = manaCost;
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

        /// <summary>What makes one monster type different from another.</summary>
        private struct MonsterSpec
        {
            public string Name;
            public CharacterAnimationSet Animations;
            public float Scale;
            public int Health;
            public int MinDamage;
            public int MaxDamage;
            public float AttackInterval;
            public int Level;
            public int Experience;
            public float MoveSpeed;
            public Vector2Int Gold;
            public LootEntry[] Loot;
        }

        private static GameObject CreateMonsterPrefab(MonsterSpec spec, Sprite barSprite, GameObject lootBagPrefab,
            AffixCatalog affixes, Action<GameObject> addExtras = null)
        {
            var monster = new GameObject(spec.Name);
            monster.transform.localScale = new Vector3(spec.Scale, spec.Scale, 1f);

            var spriteRenderer = monster.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = spec.Animations.FindClip(CharacterAnimationSet.IdleClip).south[0];
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

            // Moved by the server, not by physics; no solid collider, so players and monsters pass through each other.
            var body = monster.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;

            monster.AddComponent<NetworkObject>();
            var motion = monster.AddComponent<MonsterMotion>();
            var motionObject = new SerializedObject(motion);
            motionObject.FindProperty("moveSpeed").floatValue = spec.MoveSpeed;
            motionObject.ApplyModifiedPropertiesWithoutUndo();
            monster.AddComponent<CharacterAnimator>().AnimationSet = spec.Animations;

            AddHealth(monster, spec.Name, false, Faction.Monster, spec.Health);
            var autoAttack = monster.AddComponent<AutoAttack>();
            var attackObject = new SerializedObject(autoAttack);
            attackObject.FindProperty("minDamage").intValue = spec.MinDamage;
            attackObject.FindProperty("maxDamage").intValue = spec.MaxDamage;
            attackObject.FindProperty("attackInterval").floatValue = spec.AttackInterval;
            attackObject.FindProperty("range").floatValue = 0.7f;
            attackObject.FindProperty("critChance").floatValue = 0.05f;
            attackObject.ApplyModifiedPropertiesWithoutUndo();

            var ai = monster.AddComponent<MonsterAI>();
            var aiObject = new SerializedObject(ai);
            aiObject.FindProperty("level").intValue = spec.Level;
            aiObject.FindProperty("experienceReward").intValue = spec.Experience;
            // For now monsters do not attack players who come near; they only fight back (with their camp) when hit.
            aiObject.FindProperty("aggroRadius").floatValue = 0f;
            aiObject.ApplyModifiedPropertiesWithoutUndo();
            // Gear is not in the table: it drops by level from the whole item catalog.
            ai.ConfigureLoot(lootBagPrefab, spec.Loot, affixes, AssetDatabase.LoadAssetAtPath<ItemCatalog>($"{ItemsFolder}/ItemCatalog.asset"));
            ai.GoldReward = spec.Gold;

            // The bar and hitbox are children, so they scale with the monster.
            AddHealthPresenter(monster, barSprite, 0.85f);
            AddHitbox(monster, new Vector2(0.4f, 0.65f), new Vector2(0f, 0.34f));
            addExtras?.Invoke(monster);

            string path = $"{MonstersFolder}/{spec.Name.Replace(" ", "")}.prefab";
            Directory.CreateDirectory(MonstersFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(monster, path);
            UnityEngine.Object.DestroyImmediate(monster);
            RegisterNetworkPrefab(prefab);
            return prefab;
        }

        private static DungeonDefinition CreateDungeon(GameObject brute, GameObject orc, GameObject boss)
        {
            var instance = new GameObject("Dungeon Instance");
            instance.AddComponent<NetworkObject>();
            instance.AddComponent<DungeonInstance>();
            Directory.CreateDirectory(Path.GetDirectoryName(DungeonInstancePrefabPath));
            GameObject instancePrefab = PrefabUtility.SaveAsPrefabAsset(instance, DungeonInstancePrefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            RegisterNetworkPrefab(instancePrefab);

            var dungeon = LoadOrCreateAsset<DungeonDefinition>(DungeonDefinitionPath);
            dungeon.displayName = "Goblin Den";
            dungeon.instancePrefab = instancePrefab;
            dungeon.floorTile = CreateTile("DungeonFloor",
                PlaceholderArtGenerator.GroundTile("DungeonFloor", new Color(0.24f, 0.22f, 0.21f), new Color(0.36f, 0.33f, 0.3f), 7));
            dungeon.wallTile = CreateTile("DungeonWall", PlaceholderArtGenerator.Rock("DungeonWall", 9), Tile.ColliderType.Grid);
            dungeon.portalSprite = PlaceholderArtGenerator.Portal("Portal");
            dungeon.packs = new[]
            {
                new MonsterPack(brute, 1, 2),
                new MonsterPack(orc, 2, 2),
                new MonsterPack(brute, 2, 1),
            };
            dungeon.bossPrefab = boss;
            dungeon.minLevel = 3;
            dungeon.key = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/DenKey.asset");
            dungeon.maxPlayers = 4;
            dungeon.monsterAggroRadius = 3.5f;
            EditorUtility.SetDirty(dungeon);
            return dungeon;
        }

        private static VendorDefinition[] CreateVendors(ItemCatalog catalog, ItemDefinition potion, ItemDefinition manaPotion)
        {
            VendorDefinition MakeVendor(string fileName, string vendorName, string title, params string[] items)
            {
                var vendor = LoadOrCreateAsset<VendorDefinition>($"{VendorsFolder}/{fileName}.asset");
                vendor.displayName = vendorName;
                vendor.title = title;
                var stock = new ItemDefinition[items.Length + 2];
                for (int i = 0; i < items.Length; i++)
                    stock[i] = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{items[i]}.asset");
                stock[items.Length] = potion;
                stock[items.Length + 1] = manaPotion;
                vendor.stock = stock;
                // Like Source's vendors: better gear is scarce (1 rare, 3 uncommon) and comes back every minute;
                // common gear and potions never run out.
                var limits = new int[stock.Length];
                for (int i = 0; i < stock.Length; i++)
                    limits[i] = stock[i] == null ? 0 : stock[i].rarity >= ItemRarity.Rare ? 1 : stock[i].rarity == ItemRarity.Uncommon ? 3 : 0;
                vendor.stockLimits = limits;
                vendor.restockSeconds = 60f;
                vendor.combinesGems = false;
                vendor.crafts = Array.Empty<ItemDefinition>();
                EditorUtility.SetDirty(vendor);
                return vendor;
            }

            return new[]
            {
                // Starting gear and one rare piece each; everything better is crafted from materials.
                Crafter(MakeVendor("WarriorVendor", "Thorin Ironhand", "Warrior weapons and armor, crafting",
                        GearFile("Bronze Sword", 1), "WarAxe", GearFile("Leather Cap", 1), GearFile("Leather Jerkin", 1), GearFile("Leather Boots", 1)),
                    catalog, WarriorClass, SourceGear.ArmorKind.Leather, SourceGear.ArmorKind.Chain, SourceGear.ArmorKind.Plate),
                Crafter(MakeVendor("RangerVendor", "Sylva Swiftarrow", "Ranger bows and leather, crafting",
                        GearFile("Aspen Bow", 1), "ElvenBow", GearFile("Leather Cap", 1), GearFile("Leather Jerkin", 1), GearFile("Leather Boots", 1)),
                    catalog, RangerClass, SourceGear.ArmorKind.Leather, SourceGear.ArmorKind.Chain),
                Crafter(MakeVendor("MageVendor", "Elara the Arcanist", "Mage staves and robes, crafting",
                        GearFile("Aspen Staff", 1), "StaffOfEmbers", GearFile("Linen Hood", 1), GearFile("Linen Vestments", 1), GearFile("Linen Slippers", 1)),
                    catalog, MageClass, SourceGear.ArmorKind.Robe),
                Jeweler(MakeVendor("JewelerVendor", "Gemma Brightstone", "Jeweler: gems, gem combining",
                    "ChippedRuby", "ChippedTopaz", "ChippedSapphire", "ChippedEmerald")),
            };
        }

        /// <summary>
        /// Lets a vendor craft its class's weapons and the armor of <paramref name="armorKinds"/> (Source's desirable
        /// armor of the class) from materials.
        /// </summary>
        private static VendorDefinition Crafter(VendorDefinition vendor, ItemCatalog catalog, int classIndex, params SourceGear.ArmorKind[] armorKinds)
        {
            var crafts = new List<ItemDefinition>();
            foreach (ItemDefinition item in catalog.items)
            {
                if (item == null || !item.IsCraftable) continue;
                bool classWeapon = item.slot == ItemSlot.Weapon && item.requiredClass == classIndex;
                bool armor = GearArmorKinds.TryGetValue(item, out SourceGear.ArmorKind kind) && Array.IndexOf(armorKinds, kind) >= 0;
                if (classWeapon || armor) crafts.Add(item);
            }
            vendor.crafts = crafts.ToArray();
            EditorUtility.SetDirty(vendor);
            return vendor;
        }

        private static VendorDefinition Jeweler(VendorDefinition vendor)
        {
            vendor.combinesGems = true;
            EditorUtility.SetDirty(vendor);
            return vendor;
        }

        private static void PlaceVendors(Grid grid, VendorDefinition[] vendors)
        {
            // In St. Revere, where Source's heavy armor, ranged, staves merchants and jeweler stand.
            Vector3Int[] places = SourceStartArea.Vendors;
            var cloaks = new[] { new Color(0.55f, 0.15f, 0.12f), new Color(0.15f, 0.4f, 0.18f), new Color(0.3f, 0.2f, 0.6f), new Color(0.2f, 0.55f, 0.6f) };
            var parent = new GameObject("Town Vendors").transform;

            for (int i = 0; i < vendors.Length; i++)
            {
                var npc = new GameObject($"Vendor {i + 1} {vendors[i].displayName}");
                npc.transform.SetParent(parent, false);
                npc.transform.position = grid.GetCellCenterWorld(places[i]);

                var spriteRenderer = npc.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = PlaceholderArtGenerator.CharacterFrame(VendorArtFolder, $"Vendor_{i + 1}", cloaks[i], Vector2.down, 0f, 0f);
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;

                // Clicked to trade; does not block walking.
                var box = npc.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(0.4f, 0.7f);
                box.offset = new Vector2(0f, 0.35f);
                npc.AddComponent<Vendor>().Configure(vendors[i]);
            }
        }

        /// <summary>
        /// Source's named places of the start region, with a waystone where Source has a waypoint (and in town).
        /// Waystone ids are saved with characters: keep them, only add new ones.
        /// </summary>
        private static void PlaceZonesAndWaystones(Grid grid, Tilemap obstacles, Sprite waystoneSprite)
        {
            // One cell step is about 0.56 world units diagonally.
            const float cellToWorld = 0.56f;
            var zones = new GameObject("Zones").transform;
            foreach ((string name, Vector3Int cell, float radius) in SourceStartArea.OtherZones)
            {
                var zone = new GameObject($"Zone {name}");
                zone.transform.SetParent(zones, false);
                zone.transform.position = grid.GetCellCenterWorld(cell);
                zone.AddComponent<Zone>().Configure(name, radius * cellToWorld);
            }

            var places = SourceStartArea.Places;
            var stones = new GameObject("Waystones").transform;
            for (int i = 0; i < places.Length; i++)
            {
                var zone = new GameObject($"Zone {places[i].name}");
                zone.transform.SetParent(zones, false);
                zone.transform.position = grid.GetCellCenterWorld(places[i].cell);
                zone.AddComponent<Zone>().Configure(places[i].name, places[i].zoneRadius * cellToWorld);

                // At the start the stone stands at the corner of the square, not in the middle of it.
                Vector3Int stoneCell = places[i].cell == StartCell ? StartCell + new Vector3Int(-3, 3, 0) : OpenCellNear(obstacles, places[i].cell);
                var stone = new GameObject($"Waystone {i} {places[i].name}");
                stone.transform.SetParent(stones, false);
                stone.transform.position = grid.GetCellCenterWorld(stoneCell);
                var spriteRenderer = stone.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = waystoneSprite;
                spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
                // Clicked to travel; does not block walking.
                var box = stone.AddComponent<BoxCollider2D>();
                box.isTrigger = true;
                box.size = new Vector2(0.45f, 0.8f);
                box.offset = new Vector2(0f, 0.4f);
                stone.AddComponent<Waystone>().Configure(i, places[i].name);
            }
        }

        private static void PlaceDungeon(Grid grid, Tilemap obstacles, DungeonDefinition dungeon)
        {
            new GameObject("Dungeon Manager").AddComponent<DungeonManager>().Configure(dungeon);

            // The entrance stands where Source has Wolf's Den.
            var entrance = new GameObject("Dungeon Entrance");
            entrance.transform.position = grid.GetCellCenterWorld(OpenCellNear(obstacles, SourceStartArea.DungeonEntrance));
            var visual = new GameObject("Visual").AddComponent<SpriteRenderer>();
            visual.transform.SetParent(entrance.transform, false);
            visual.sprite = dungeon.portalSprite;
            visual.spriteSortPoint = SpriteSortPoint.Pivot;
            var box = entrance.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(0.6f, 0.9f);
            box.offset = new Vector2(0f, 0.45f);
            entrance.AddComponent<Portal>().Configure(PortalKind.DungeonEntrance, visual, null, false);
        }

        private static GameObject CreateLootBagPrefab(Sprite sprite)
        {
            var loot = new GameObject("Loot Bag");
            var spriteRenderer = loot.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            loot.AddComponent<NetworkObject>();
            loot.AddComponent<LootBag>();
            AddHitbox(loot, new Vector2(0.35f, 0.3f), new Vector2(0f, 0.13f));

            Directory.CreateDirectory(Path.GetDirectoryName(LootBagPrefabPath));
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(loot, LootBagPrefabPath);
            UnityEngine.Object.DestroyImmediate(loot);
            RegisterNetworkPrefab(prefab);
            return prefab;
        }

        private static ItemCatalog CreateItems(out ItemDefinition potion, out ItemDefinition manaPotion)
        {
            var metal = new Color(0.62f, 0.64f, 0.7f);
            var leather = new Color(0.55f, 0.38f, 0.22f);
            var orcish = new Color(0.45f, 0.3f, 0.25f);
            var steel = new Color(0.75f, 0.78f, 0.85f);
            var wood = new Color(0.6f, 0.45f, 0.28f);
            var cloth = new Color(0.55f, 0.4f, 0.7f);
            var arcane = new Color(0.45f, 0.35f, 0.9f);

            potion = LoadOrCreateAsset<ItemDefinition>($"{ItemsFolder}/HealingPotion.asset");
            potion.displayName = "Healing Potion";
            potion.slot = ItemSlot.None;
            potion.rarity = ItemRarity.Common;
            potion.damageBonus = potion.healthBonus = potion.armor = 0;
            potion.use = ItemUse.Heal;
            potion.healFraction = 0.35f;
            potion.maxStack = 10;
            potion.price = 10;
            potion.requiredClass = -1;
            potion.iconColor = new Color(0.85f, 0.15f, 0.2f);
            potion.canHaveAffix = false;
            EditorUtility.SetDirty(potion);

            manaPotion = CreateItem("ManaPotion", "Mana Potion", ItemSlot.None, ItemRarity.Common, 0, 0, 0, new Color(0.2f, 0.35f, 0.95f), 10);
            manaPotion.use = ItemUse.RestoreMana;
            manaPotion.healFraction = 0.4f;
            manaPotion.maxStack = 10;
            manaPotion.canHaveAffix = false;

            var catalog = LoadOrCreateAsset<ItemCatalog>($"{ItemsFolder}/ItemCatalog.asset");
            // Players will store item ids (catalog index + 1): only ever append to this list.
            catalog.items = new[]
            {
                potion,
                // Dropped by monsters; any class can use them.
                CreateItem("RustySword", "Rusty Sword", ItemSlot.Weapon, ItemRarity.Common, 2, 0, 0, metal * 0.8f, 8, itemLevel: 1),
                CreateItem("IronSword", "Iron Sword", ItemSlot.Weapon, ItemRarity.Uncommon, 4, 0, 0, metal, 40, itemLevel: 3),
                CreateItem("GoblinCleaver", "Goblin Cleaver", ItemSlot.Weapon, ItemRarity.Rare, 7, 10, 0, new Color(0.5f, 0.6f, 0.4f), 160, itemLevel: 5),
                CreateItem("OrcishGreataxe", "Orcish Greataxe", ItemSlot.Weapon, ItemRarity.Epic, 12, 20, 0, orcish, 500, itemLevel: 8),
                CreateItem("LeatherCap", "Leather Cap", ItemSlot.Head, ItemRarity.Common, 0, 0, 4, leather, 6, itemLevel: 1),
                CreateItem("IronHelm", "Iron Helm", ItemSlot.Head, ItemRarity.Uncommon, 0, 5, 8, metal, 40, itemLevel: 3),
                CreateItem("OrcSkullHelm", "Orc Skull Helm", ItemSlot.Head, ItemRarity.Rare, 0, 15, 12, new Color(0.85f, 0.82f, 0.7f), 150, itemLevel: 6),
                CreateItem("PaddedVest", "Padded Vest", ItemSlot.Chest, ItemRarity.Common, 0, 0, 6, leather, 10, itemLevel: 1),
                CreateItem("Chainmail", "Chainmail", ItemSlot.Chest, ItemRarity.Uncommon, 0, 10, 12, metal, 60, itemLevel: 4),
                CreateItem("OrcishPlate", "Orcish Plate", ItemSlot.Chest, ItemRarity.Epic, 0, 30, 22, orcish, 500, itemLevel: 7),
                CreateItem("WornBoots", "Worn Boots", ItemSlot.Feet, ItemRarity.Common, 0, 0, 3, leather, 5, itemLevel: 1),
                CreateItem("IronBoots", "Iron Boots", ItemSlot.Feet, ItemRarity.Uncommon, 0, 5, 6, metal, 35, itemLevel: 3),

                // Sold by the class vendors in town.
                CreateItem("MilitiaSword", "Militia Sword", ItemSlot.Weapon, ItemRarity.Common, 3, 0, 0, metal * 0.9f, 30, WarriorClass, itemLevel: 1),
                CreateItem("SteelLongsword", "Steel Longsword", ItemSlot.Weapon, ItemRarity.Uncommon, 6, 0, 0, metal, 120, WarriorClass, itemLevel: 4),
                CreateItem("WarAxe", "War Axe", ItemSlot.Weapon, ItemRarity.Rare, 10, 10, 0, steel, 400, WarriorClass, itemLevel: 6),
                CreateItem("SteelHelm", "Steel Helm", ItemSlot.Head, ItemRarity.Uncommon, 0, 5, 10, steel, 110, WarriorClass, itemLevel: 4),
                CreateItem("SteelBreastplate", "Steel Breastplate", ItemSlot.Chest, ItemRarity.Uncommon, 0, 10, 16, steel, 160, WarriorClass, itemLevel: 5),
                CreateItem("SteelSabatons", "Steel Sabatons", ItemSlot.Feet, ItemRarity.Uncommon, 0, 0, 8, steel, 90, WarriorClass, itemLevel: 4),

                CreateItem("HuntingBow", "Hunting Bow", ItemSlot.Weapon, ItemRarity.Common, 3, 0, 0, wood, 30, RangerClass, itemLevel: 1),
                CreateItem("Longbow", "Longbow", ItemSlot.Weapon, ItemRarity.Uncommon, 6, 0, 0, wood, 120, RangerClass, itemLevel: 4),
                CreateItem("ElvenBow", "Elven Bow", ItemSlot.Weapon, ItemRarity.Rare, 10, 10, 0, new Color(0.55f, 0.75f, 0.45f), 400, RangerClass, itemLevel: 6),
                CreateItem("LeatherHood", "Leather Hood", ItemSlot.Head, ItemRarity.Common, 0, 0, 3, leather, 20, RangerClass, itemLevel: 1),
                CreateItem("StuddedJerkin", "Studded Jerkin", ItemSlot.Chest, ItemRarity.Uncommon, 0, 10, 10, leather, 130, RangerClass, itemLevel: 4),
                CreateItem("RangerBoots", "Ranger Boots", ItemSlot.Feet, ItemRarity.Uncommon, 0, 5, 5, leather, 80, RangerClass, itemLevel: 3),

                CreateItem("ApprenticeStaff", "Apprentice Staff", ItemSlot.Weapon, ItemRarity.Common, 3, 0, 0, wood, 30, MageClass, itemLevel: 1),
                CreateItem("RunedStaff", "Runed Staff", ItemSlot.Weapon, ItemRarity.Uncommon, 6, 5, 0, arcane, 120, MageClass, itemLevel: 4),
                CreateItem("StaffOfEmbers", "Staff of Embers", ItemSlot.Weapon, ItemRarity.Rare, 10, 10, 0, new Color(0.9f, 0.4f, 0.15f), 400, MageClass, itemLevel: 6),
                CreateItem("SilkHood", "Silk Hood", ItemSlot.Head, ItemRarity.Common, 0, 5, 2, cloth, 20, MageClass, itemLevel: 1),
                CreateItem("ApprenticeRobe", "Apprentice Robe", ItemSlot.Chest, ItemRarity.Common, 0, 8, 4, cloth, 35, MageClass, itemLevel: 1),
                CreateItem("EnchantedRobe", "Enchanted Robe", ItemSlot.Chest, ItemRarity.Uncommon, 0, 20, 7, arcane, 140, MageClass, itemLevel: 4),
                CreateItem("SilkSlippers", "Silk Slippers", ItemSlot.Feet, ItemRarity.Common, 0, 4, 2, cloth, 15, MageClass, itemLevel: 1),

                // Junk dropped by monsters: only worth selling.
                CreateJunk("GoblinEar", "Goblin Ear", new Color(0.45f, 0.6f, 0.3f), 8),
                CreateJunk("BruteTusk", "Brute Tusk", new Color(0.9f, 0.88f, 0.75f), 24),
                CreateJunk("OrcFang", "Orc Fang", new Color(0.8f, 0.78f, 0.65f), 40),
                CreateJunk("CrownShard", "Crown Shard", new Color(0.85f, 0.7f, 0.2f), 200),
                manaPotion,
                CreateKey("DenKey", "Den Key", new Color(0.85f, 0.65f, 0.25f), 20),
            };
            // Gems: three of a kind combine into the next tier at the jeweler.
            var gems = new List<ItemDefinition>();
            foreach ((string gemName, RPG.Progression.Attribute attribute, Color color) in new[]
                     {
                         ("Ruby", RPG.Progression.Attribute.Strength, new Color(0.85f, 0.1f, 0.15f)),
                         ("Topaz", RPG.Progression.Attribute.Agility, new Color(0.95f, 0.65f, 0.15f)),
                         ("Sapphire", RPG.Progression.Attribute.Intelligence, new Color(0.15f, 0.35f, 0.95f)),
                         ("Emerald", RPG.Progression.Attribute.Vitality, new Color(0.15f, 0.75f, 0.3f)),
                     })
            {
                ItemDefinition flawless = CreateGem($"Flawless{gemName}", $"Flawless {gemName}", ItemRarity.Epic, attribute, 7, color * 1.15f, 600, null);
                ItemDefinition normal = CreateGem(gemName, gemName, ItemRarity.Rare, attribute, 4, color, 200, flawless);
                ItemDefinition chipped = CreateGem($"Chipped{gemName}", $"Chipped {gemName}", ItemRarity.Uncommon, attribute, 2, color * 0.8f, 60, normal);
                gems.Add(chipped);
                gems.Add(normal);
                gems.Add(flawless);
            }
            var all = new List<ItemDefinition>(catalog.items);
            // The hand-made gear is only sold now: drops by level come from Source's gear below.
            foreach (ItemDefinition item in all)
                if (item.IsEquippable) item.dropWeight = 0f;
            all.AddRange(gems);
            all.AddRange(CreateSourceGear(out List<ItemDefinition> materials));
            all.AddRange(materials);
            catalog.items = all.ToArray();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static readonly Color[] MetalColors =
        {
            new Color(0.72f, 0.48f, 0.25f), new Color(0.7f, 0.73f, 0.78f), new Color(0.55f, 0.75f, 0.9f), new Color(0.2f, 0.7f, 0.4f),
            new Color(0.75f, 0.9f, 1f), new Color(0.92f, 0.95f, 1f), new Color(0.5f, 0.5f, 0.58f),
        };

        private static readonly Color[] WoodColors =
        {
            new Color(0.82f, 0.72f, 0.52f), new Color(0.65f, 0.5f, 0.32f), new Color(0.62f, 0.3f, 0.22f), new Color(0.32f, 0.22f, 0.15f),
            new Color(0.88f, 0.82f, 0.68f), new Color(0.75f, 0.58f, 0.36f), new Color(0.55f, 0.4f, 0.25f),
        };

        private static Color ArmorColor(int armorType)
        {
            switch (SourceGear.KindOf(armorType))
            {
                case SourceGear.ArmorKind.Leather: return new Color(0.55f, 0.38f, 0.22f) * (0.9f + 0.1f * (armorType - 2));
                case SourceGear.ArmorKind.Chain:
                case SourceGear.ArmorKind.Plate: return MetalColors[armorType - 5];
                case SourceGear.ArmorKind.Robe:
                    return new[] { new Color(0.85f, 0.8f, 0.7f), new Color(0.6f, 0.55f, 0.5f), new Color(0.7f, 0.5f, 0.85f), new Color(0.5f, 0.8f, 0.95f) }[armorType - 12];
                default: return new Color(0.8f, 0.8f, 0.75f);
            }
        }

        /// <summary>Asset file name of a generated piece, e.g. "Source/SteelSword_L04".</summary>
        private static string GearFile(string displayName, int level) => $"Source/{displayName.Replace(" ", "")}_L{level:00}";

        /// <summary>What kind of armor each generated armor piece is (which class vendor crafts it); weapons are not in it.</summary>
        private static readonly Dictionary<ItemDefinition, SourceGear.ArmorKind> GearArmorKinds =
            new Dictionary<ItemDefinition, SourceGear.ArmorKind>();

        /// <summary>How many of its material a piece takes to craft.</summary>
        private static int CraftCount(ItemSlot slot) => slot == ItemSlot.Weapon || slot == ItemSlot.Chest ? 6 : 4;

        /// <summary>A crafting material: rarer (colored) the later it starts dropping.</summary>
        private static ItemDefinition CreateMaterial(string displayName, int firstLevel, Color color)
        {
            ItemRarity rarity = firstLevel < 6 ? ItemRarity.Common : firstLevel < 13 ? ItemRarity.Uncommon
                : firstLevel < 20 ? ItemRarity.Rare : ItemRarity.Epic;
            ItemDefinition material = CreateItem($"Source/Materials/{displayName.Replace(" ", "")}", displayName, ItemSlot.None,
                rarity, 0, 0, 0, color, 2 + firstLevel);
            material.maxStack = 50;
            material.canHaveAffix = false;
            return material;
        }

        private static int FirstLevel((int key, int level, float chance)[] table, int key)
        {
            int first = 25;
            foreach ((int tableKey, int level, float _) in table)
                if (tableKey == key) first = Mathf.Min(first, level);
            return first;
        }

        /// <summary>
        /// Source's gear (<see cref="SourceGear"/>): for every level, each material its chance tables can drop
        /// there, as a sword (warriors), bow (rangers) and staff (mages), and as head, chest and feet armor anyone may
        /// wear. Stats grow with Source's sell price of the piece, the drop weight is its material chance.
        /// Every piece is crafted from its material (ingots, logs, hides, cloth), which is what monsters drop.
        /// </summary>
        private static List<ItemDefinition> CreateSourceGear(out List<ItemDefinition> materials)
        {
            GearArmorKinds.Clear();
            var gear = new List<ItemDefinition>();
            materials = new List<ItemDefinition>();
            var ingots = new ItemDefinition[SourceGear.Metals.Length];
            var logs = new ItemDefinition[SourceGear.Woods.Length];
            for (int tier = 1; tier <= ingots.Length; tier++)
            {
                (int, int, float)[] weaponTable = SourceGear.WeaponChances;
                ingots[tier - 1] = CreateMaterial($"{SourceGear.Metals[tier - 1]} Ingot", FirstLevel(weaponTable, tier), MetalColors[tier - 1]);
                logs[tier - 1] = CreateMaterial($"{SourceGear.Woods[tier - 1]} Log", FirstLevel(weaponTable, tier), WoodColors[tier - 1]);
            }
            materials.AddRange(ingots);
            materials.AddRange(logs);
            // Armor: hides for leather, the metal ingots for chain (Bronze-Emerald) and plate (Crystal-Titanium), cloth for robes.
            var armorMaterials = new ItemDefinition[SourceGear.ArmorMaterials.Length];
            for (int type = 2; type < armorMaterials.Length; type++)
            {
                SourceGear.ArmorKind kind = SourceGear.KindOf(type);
                if (kind == SourceGear.ArmorKind.Chain || kind == SourceGear.ArmorKind.Plate)
                {
                    armorMaterials[type] = ingots[type - 5];
                    continue;
                }
                string name = SourceGear.ArmorMaterials[type] + (kind == SourceGear.ArmorKind.Leather ? " Hide" : " Cloth");
                armorMaterials[type] = CreateMaterial(name, FirstLevel(SourceGear.ArmorChances, type), ArmorColor(type));
                materials.Add(armorMaterials[type]);
            }

            ItemDefinition Piece(string displayName, ItemSlot slot, int level, float chance, int damage, int armor, int mana,
                Color color, float SourceSellPrice, int requiredClass, ItemDefinition material)
            {
                // Our gold is about a tenth of theirs: vendors pay a quarter of the price.
                int price = Mathf.Max(1, Mathf.RoundToInt(SourceSellPrice * 0.4f));
                ItemDefinition item = CreateItem(GearFile(displayName, level), displayName, slot, ItemRarity.Common, damage, 0, armor,
                    color, price, requiredClass, level);
                item.manaBonus = mana;
                item.dropWeight = chance / 100f;
                item.craftMaterial = material;
                item.craftCount = CraftCount(slot);
                gear.Add(item);
                return item;
            }

            foreach ((int tier, int level, float chance) in SourceGear.WeaponChances)
            {
                int sell = SourceGear.WeaponSellPrice(tier, level);
                int damage = Mathf.RoundToInt(2f + (sell - 50) * 0.035f);
                Piece($"{SourceGear.Metals[tier - 1]} Sword", ItemSlot.Weapon, level, chance, damage, 0, 0, MetalColors[tier - 1], sell,
                    WarriorClass, ingots[tier - 1]);
                Piece($"{SourceGear.Woods[tier - 1]} Bow", ItemSlot.Weapon, level, chance, damage, 0, 0, WoodColors[tier - 1], sell * 1.1f,
                    RangerClass, logs[tier - 1]);
                Piece($"{SourceGear.Woods[tier - 1]} Staff", ItemSlot.Weapon, level, chance, damage, 0, Mathf.RoundToInt(damage * 0.5f),
                    WoodColors[tier - 1] * 0.9f, sell * 1.1f, MageClass, logs[tier - 1]);
            }

            foreach ((int type, int level, float chance) in SourceGear.ArmorChances)
            {
                float sell = SourceGear.ArmorSellPrice(type, level);
                float power = sell - 50f;
                bool robe = SourceGear.KindOf(type) == SourceGear.ArmorKind.Robe;
                // Robes cost like armor but give mana: little armor, a mana pool that grows with their price.
                float armor = robe ? 1f + level * 0.15f : 3f + power * 0.06f;
                float mana = robe ? 4f + power * 0.12f : 0f;
                (string head, string chest, string feet) = SourceGear.NamesOf(SourceGear.KindOf(type));
                string material = SourceGear.ArmorMaterials[type];
                Color color = ArmorColor(type);
                foreach ((string baseName, ItemSlot slot, float share) in new[] { (head, ItemSlot.Head, 0.7f), (chest, ItemSlot.Chest, 1f), (feet, ItemSlot.Feet, 0.5f) })
                {
                    ItemDefinition piece = Piece($"{material} {baseName}", slot, level, chance, 0, Mathf.Max(1, Mathf.RoundToInt(armor * share)),
                        Mathf.RoundToInt(mana * share), color, sell * (0.6f + 0.4f * share), -1, armorMaterials[type]);
                    GearArmorKinds[piece] = SourceGear.KindOf(type);
                }
            }
            return gear;
        }

        /// <summary>Loot entries by item asset file name.</summary>
        private static LootEntry[] Loot(ItemCatalog catalog, params (string fileName, float chance)[] drops)
        {
            var entries = new LootEntry[drops.Length];
            for (int i = 0; i < drops.Length; i++)
            {
                var item = AssetDatabase.LoadAssetAtPath<ItemDefinition>($"{ItemsFolder}/{drops[i].fileName}.asset");
                if (item == null) Debug.LogError($"[TestScene] Loot item {drops[i].fileName} not found.");
                entries[i] = new LootEntry(item, drops[i].chance);
            }
            return entries;
        }

        private static ItemDefinition CreateItem(string fileName, string displayName, ItemSlot slot, ItemRarity rarity,
            int damageBonus, int healthBonus, int armor, Color iconColor, int price, int requiredClass = -1, int itemLevel = 1)
        {
            var item = LoadOrCreateAsset<ItemDefinition>($"{ItemsFolder}/{fileName}.asset");
            item.displayName = displayName;
            item.slot = slot;
            item.rarity = rarity;
            item.damageBonus = damageBonus;
            item.healthBonus = healthBonus;
            item.armor = armor;
            item.use = ItemUse.None;
            item.maxStack = 1;
            item.iconColor = iconColor;
            item.price = price;
            item.requiredClass = requiredClass;
            item.itemLevel = itemLevel;
            item.manaBonus = 0;
            item.attributes = default;
            item.canHaveAffix = true;
            item.isGem = false;
            item.combinesInto = null;
            item.dropWeight = 1f;
            EditorUtility.SetDirty(item);
            return item;
        }

        private static ItemDefinition CreateJunk(string fileName, string displayName, Color iconColor, int price)
        {
            ItemDefinition item = CreateItem(fileName, displayName, ItemSlot.None, ItemRarity.Poor, 0, 0, 0, iconColor, price);
            item.maxStack = 20;
            item.canHaveAffix = false;
            return item;
        }

        private static ItemDefinition CreateGem(string fileName, string displayName, ItemRarity rarity, RPG.Progression.Attribute attribute,
            int amount, Color iconColor, int price, ItemDefinition combinesInto)
        {
            ItemDefinition gem = CreateItem(fileName, displayName, ItemSlot.None, rarity, 0, 0, 0, iconColor, price);
            var attributes = new AttributeSet();
            attributes[attribute] = amount;
            gem.attributes = attributes;
            gem.maxStack = 10;
            gem.canHaveAffix = false;
            gem.isGem = true;
            gem.combinesInto = combinesInto;
            return gem;
        }

        private static ItemDefinition CreateKey(string fileName, string displayName, Color iconColor, int price)
        {
            ItemDefinition item = CreateItem(fileName, displayName, ItemSlot.None, ItemRarity.Rare, 0, 0, 0, iconColor, price);
            item.maxStack = 5;
            item.canHaveAffix = false;
            return item;
        }

        private static AffixCatalog CreateAffixes()
        {
            AffixDefinition Affix(string fileName, string displayName, bool isPrefix, ItemRarity quality, int minLevel, int maxLevel,
                AttributeSet attributes, int damage = 0, int health = 0, int armor = 0, int mana = 0)
            {
                var affix = LoadOrCreateAsset<AffixDefinition>($"{AffixesFolder}/{fileName}.asset");
                affix.displayName = displayName;
                affix.isPrefix = isPrefix;
                affix.quality = quality;
                affix.minLevel = minLevel;
                affix.maxLevel = maxLevel;
                affix.attributes = attributes;
                affix.damageBonus = damage;
                affix.healthBonus = health;
                affix.armor = armor;
                affix.manaBonus = mana;
                affix.priceMultiplier = quality switch
                {
                    ItemRarity.Uncommon => 1.5f,
                    ItemRarity.Rare => 2.5f,
                    ItemRarity.Epic => 4f,
                    _ => 8f,
                };
                EditorUtility.SetDirty(affix);
                return affix;
            }

            var catalog = LoadOrCreateAsset<AffixCatalog>($"{AffixesFolder}/AffixCatalog.asset");
            // Items store affix ids (index + 1): only ever append to this list.
            catalog.affixes = new[]
            {
                Affix("Sturdy", "Sturdy", true, ItemRarity.Uncommon, 1, 5, default, armor: 2),
                Affix("Sharp", "Sharp", true, ItemRarity.Uncommon, 1, 5, default, damage: 1),
                Affix("OfTheBear", "of the Bear", false, ItemRarity.Uncommon, 1, 99, new AttributeSet(2, 0, 0, 2)),
                Affix("OfTheFox", "of the Fox", false, ItemRarity.Uncommon, 1, 99, new AttributeSet(0, 3, 0, 1)),
                Affix("OfTheOwl", "of the Owl", false, ItemRarity.Uncommon, 1, 99, new AttributeSet(0, 0, 3, 1)),
                Affix("Reinforced", "Reinforced", true, ItemRarity.Rare, 3, 99, new AttributeSet(0, 0, 0, 3), armor: 4),
                Affix("Keen", "Keen", true, ItemRarity.Rare, 3, 99, new AttributeSet(0, 2, 0, 0), damage: 3),
                Affix("Arcane", "Arcane", true, ItemRarity.Rare, 3, 99, new AttributeSet(0, 0, 4, 0), mana: 15),
                Affix("OfTheTiger", "of the Tiger", false, ItemRarity.Rare, 3, 99, new AttributeSet(4, 4, 0, 0)),
                Affix("OfTheWhale", "of the Whale", false, ItemRarity.Rare, 3, 99, new AttributeSet(0, 0, 0, 5), health: 10),
                Affix("Brutal", "Brutal", true, ItemRarity.Epic, 5, 99, new AttributeSet(6, 0, 0, 0), damage: 4),
                Affix("OfTheSorcerer", "of the Sorcerer", false, ItemRarity.Epic, 5, 99, new AttributeSet(0, 0, 8, 2), mana: 25),
                Affix("OfTheGuardian", "of the Guardian", false, ItemRarity.Epic, 5, 99, new AttributeSet(0, 0, 0, 8), health: 20, armor: 6),
                Affix("Kingslayer", "Kingslayer's", true, ItemRarity.Legendary, 7, 99, new AttributeSet(6, 6, 6, 6), damage: 6, armor: 5),
            };
            catalog.affixChance = 0.6f;
            EditorUtility.SetDirty(catalog);
            return catalog;
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

        /// <summary>
        /// One monster on every spot Source has one between the start and St. Revere, coming back as fast as there.
        /// Source levels 1-2 look like goblins, 3-5 brutes, higher orcs, and keep the Source level (health, damage,
        /// experience and gear drops follow it); its elites and bosses are elites here. None inside the towns.
        /// </summary>
        private static void PlaceMonsters(Grid grid, Tilemap obstacles, GameObject goblin, GameObject brute, GameObject orc)
        {
            var parent = new GameObject("Monster Spots").transform;
            var monsters = SourceStartArea.Monsters;
            for (int i = 0; i < monsters.Length; i++)
            {
                (Vector3Int cell, int level, bool elite, float respawn) = monsters[i];
                if (IsCleared(cell, 0)) continue;
                GameObject prefab = level <= 2 ? goblin : level <= 5 ? brute : orc;
                var spot = new GameObject($"Monster Spot {i + 1} {prefab.name}{(elite ? " (elite)" : "")}");
                spot.transform.SetParent(parent, false);
                spot.transform.position = grid.GetCellCenterWorld(OpenCellNear(obstacles, cell));
                var spawner = spot.AddComponent<MonsterSpawner>();
                spawner.Configure(prefab, 1, 0.6f, respawn);
                spawner.EliteChance = elite ? 1f : 0f;
                spawner.Level = level;
            }
        }

        private struct ChestPrefabs
        {
            public GameObject Crate;
            public GameObject Chest;
            public GameObject Strongbox;
        }

        private static ChestPrefabs CreateChestPrefabs(ItemCatalog items, AffixCatalog affixes, GameObject lootBagPrefab)
        {
            var wood = new Color(0.55f, 0.38f, 0.22f);
            var darkWood = new Color(0.42f, 0.26f, 0.14f);
            var iron = new Color(0.45f, 0.47f, 0.52f);
            var gold = new Color(0.95f, 0.78f, 0.3f);
            return new ChestPrefabs
            {
                Crate = CreateChestPrefab("WoodenCrate", "Wooden Crate", 2, new Vector2Int(3, 8),
                    PlaceholderArtGenerator.Container("WoodenCrate", wood, darkWood, darkWood, 52f, 34f, false), lootBagPrefab, affixes,
                    Loot(items, ("GoblinEar", 0.5f), ("HealingPotion", 0.5f), ("ManaPotion", 0.4f)), items, 1),
                Chest = CreateChestPrefab("OakChest", "Oak Chest", 4, new Vector2Int(10, 25),
                    PlaceholderArtGenerator.Container("OakChest", darkWood, iron, gold, 60f, 32f, true), lootBagPrefab, affixes,
                    Loot(items, ("HealingPotion", 0.7f), ("ManaPotion", 0.6f), ("DenKey", 0.15f), ("ChippedRuby", 0.1f), ("ChippedTopaz", 0.1f),
                        ("ChippedSapphire", 0.1f), ("ChippedEmerald", 0.1f)), items, 2),
                Strongbox = CreateChestPrefab("IronStrongbox", "Iron Strongbox", 6, new Vector2Int(30, 60),
                    PlaceholderArtGenerator.Container("IronStrongbox", iron, new Color(0.3f, 0.31f, 0.35f), gold, 56f, 36f, false), lootBagPrefab, affixes,
                    Loot(items, ("DenKey", 0.3f), ("ChippedRuby", 0.25f), ("ChippedTopaz", 0.25f), ("ChippedSapphire", 0.25f),
                        ("ChippedEmerald", 0.25f), ("Ruby", 0.05f), ("Emerald", 0.05f)), items, 3),
            };
        }

        private static GameObject CreateChestPrefab(string fileName, string chestName, int level, Vector2Int gold, Sprite sprite,
            GameObject lootBagPrefab, AffixCatalog affixes, LootEntry[] loot, ItemCatalog gear, int gearRolls)
        {
            var chest = new GameObject(chestName);
            var spriteRenderer = chest.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            chest.AddComponent<NetworkObject>();
            chest.AddComponent<LootChest>().Configure(chestName, level, loot, gold, lootBagPrefab, affixes, gear, gearRolls);
            // Clicked to open; does not block walking.
            AddHitbox(chest, new Vector2(0.45f, 0.35f), new Vector2(0f, 0.15f));

            string path = $"{ChestPrefabFolder}/{fileName}.prefab";
            Directory.CreateDirectory(ChestPrefabFolder);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(chest, path);
            UnityEngine.Object.DestroyImmediate(chest);
            RegisterNetworkPrefab(prefab);
            return prefab;
        }

        /// <summary>
        /// A container on every spot Source has one in its start region, back after the same time
        /// (crates and barrels 60 s, chests 5 min). Crates and barrels become wooden crates; chests up to level 4
        /// oak chests, higher ones iron strongboxes.
        /// </summary>
        private static void PlaceChests(Grid grid, Tilemap obstacles, ChestPrefabs chests)
        {
            var parent = new GameObject("Chests").transform;
            var spots = SourceStartArea.Chests;
            for (int i = 0; i < spots.Length; i++)
            {
                (Vector3Int cell, int kind, int level, float respawn) = spots[i];
                GameObject prefab = kind == 0 ? chests.Crate : level <= 4 ? chests.Chest : chests.Strongbox;
                var spawner = new GameObject($"Chest Spot {i + 1}");
                spawner.transform.SetParent(parent, false);
                spawner.transform.position = grid.GetCellCenterWorld(OpenCellNear(obstacles, cell));
                spawner.AddComponent<ChestSpawner>().Configure(prefab, respawn, level);
            }
        }

        private static void PlaceDummies(Grid grid, GameObject dummyPrefab)
        {
            Vector3Int start = StartCell;
            // Right and left of the start, on grass inside the cleared start area.
            var cells = new[] { new Vector3Int(start.x + 2, start.y - 2, 0), new Vector3Int(start.x - 3, start.y - 1, 0) };
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

            // Names over units first, so frames and windows are drawn over them.
            var unitNameplates = new GameObject("Unit Nameplates", typeof(RectTransform));
            unitNameplates.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)unitNameplates.transform);
            unitNameplates.AddComponent<UnitNameplates>();

            // These build their own contents at runtime. Layout: player top left, target top middle,
            // minimap top right, messages bottom left, action bar and experience bottom middle.
            CreateUnitFrame(canvasObject.transform, "Player Frame", UnitFrameRole.LocalPlayer, new Vector2(0f, 1f), new Vector2(24f, -16f));
            CreateUnitFrame(canvasObject.transform, "Target Frame", UnitFrameRole.Target, new Vector2(0.5f, 1f), new Vector2(0f, -16f));
            CreateHudElement<Minimap>(canvasObject.transform, "Minimap", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -10f), new Vector2(240f, 270f));
            CreateHudElement<MessageLog>(canvasObject.transform, "Message Log", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(12f, 12f), new Vector2(480f, 180f));
            CreateHudElement<ExperienceBar>(canvasObject.transform, "Experience Bar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(520f, 12f));
            CreateHudElement<AbilityBar>(canvasObject.transform, "Ability Bar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(520f, 80f));
            var nameplates = new GameObject("NPC Nameplates", typeof(RectTransform));
            nameplates.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)nameplates.transform);
            nameplates.AddComponent<NpcNameplates>();

            var vendorWindow = new GameObject("Vendor Window", typeof(RectTransform));
            vendorWindow.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)vendorWindow.transform);
            vendorWindow.AddComponent<VendorWindow>();

            var inventoryWindow = new GameObject("Inventory Window", typeof(RectTransform));
            inventoryWindow.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)inventoryWindow.transform);
            inventoryWindow.AddComponent<InventoryWindow>();

            CreateHudElement<ZoneBanner>(canvasObject.transform, "Zone Banner", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(800f, 60f));

            var worldMap = new GameObject("World Map", typeof(RectTransform));
            worldMap.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)worldMap.transform);
            worldMap.AddComponent<WorldMap>();

            var waystoneWindow = new GameObject("Waystone Window", typeof(RectTransform));
            waystoneWindow.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)waystoneWindow.transform);
            waystoneWindow.AddComponent<WaystoneWindow>();

            var characterWindow = new GameObject("Character Window", typeof(RectTransform));
            characterWindow.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)characterWindow.transform);
            characterWindow.AddComponent<CharacterWindow>();
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
            // The frame builds its portrait and bars at runtime.
            var holder = new GameObject(frameName, typeof(RectTransform));
            holder.transform.SetParent(canvas, false);
            var holderRect = (RectTransform)holder.transform;
            holderRect.anchorMin = holderRect.anchorMax = holderRect.pivot = anchor;
            holderRect.anchoredPosition = offset;
            holderRect.sizeDelta = new Vector2(440f, 110f);

            var frame = holder.AddComponent<UnitFrame>();
            var frameObject = new SerializedObject(frame);
            frameObject.FindProperty("role").enumValueIndex = (int)role;
            frameObject.ApplyModifiedPropertiesWithoutUndo();
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
