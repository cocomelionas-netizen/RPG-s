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
    public static partial class TestSceneBuilder
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

        // The world is the size of the source game's start region, laid out from its data (StartAreaData).
        private const int MapWidth = StartAreaData.Width;
        private const int MapHeight = StartAreaData.Height;
        // New characters start in Pilgrim's Refuge; the main town with the vendors is St. Revere, like in the source game.
        private static Vector3Int StartCell => StartAreaData.Start;
        private static Vector3Int TownCell => StartAreaData.Town;
        // Paved squares, and areas without rocks or trees, around both.
        private const int TownSquareRadius = 6;
        private const int TownClearRadius = 18;
        private const int StartSquareRadius = 3;
        private const int StartClearRadius = 6;
        private const int Seed = 12345;

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
            Tile blockedTile = CreateTile("Blocked", null, Tile.ColliderType.Grid);
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
                Name = "Goblin", Animations = goblinAnimations, Scale = 0.85f, Health = SourceHealth(2), MinDamage = 6, MaxDamage = 10,
                AttackInterval = 2f, Level = 2, Experience = LevelCurve.KillExperienceFor(2), MoveSpeed = 1.8f, Gold = new Vector2Int(3, 6),
                Loot = Loot(items, ("ChippedRuby", 0.02f), ("ChippedTopaz", 0.02f), ("ChippedSapphire", 0.02f), ("ChippedEmerald", 0.02f), ("RoughAmethyst", 0.02f), ("HealingPotion", 0.3f), ("ManaPotion", 0.15f)),
            }, barSprite, lootBagPrefab, affixes);
            GameObject brutePrefab = CreateMonsterPrefab(new MonsterSpec
            {
                Name = "Goblin Brute", Animations = bruteAnimations, Scale = 1.05f, Health = SourceHealth(4), MinDamage = 12, MaxDamage = 20,
                AttackInterval = 2.2f, Level = 4, Experience = LevelCurve.KillExperienceFor(4), MoveSpeed = 1.6f, Gold = new Vector2Int(10, 16),
                Loot = Loot(items, ("ChippedRuby", 0.04f), ("ChippedTopaz", 0.04f), ("ChippedSapphire", 0.04f), ("ChippedEmerald", 0.04f), ("RoughAmethyst", 0.04f), ("HealingPotion", 0.5f), ("ManaPotion", 0.25f)),
            }, barSprite, lootBagPrefab, affixes);
            GameObject orcPrefab = CreateMonsterPrefab(new MonsterSpec
            {
                Name = "Orc", Animations = orcAnimations, Scale = 1.15f, Health = SourceHealth(6), MinDamage = 20, MaxDamage = 30,
                AttackInterval = 2f, Level = 6, Experience = LevelCurve.KillExperienceFor(6), MoveSpeed = 1.7f, Gold = new Vector2Int(20, 30),
                Loot = Loot(items, ("ChippedRuby", 0.05f), ("ChippedTopaz", 0.05f), ("ChippedSapphire", 0.05f), ("ChippedEmerald", 0.05f), ("RoughAmethyst", 0.05f), ("Ruby", 0.01f), ("Sapphire", 0.01f), ("HealingPotion", 0.6f), ("ManaPotion", 0.3f)),
            }, barSprite, lootBagPrefab, affixes);
            CharacterAnimationSet kingAnimations = CreateCharacterAnimationSet("GoblinKing", KingArtFolder, KingAnimationSetPath,
                new Color(0.75f, 0.58f, 0.15f), new Color(0.4f, 0.58f, 0.25f));
            // The source game's dungeon bosses: an orc's numbers (each boss's own health, damage, level and loot are set per
            // dungeon spot, measured against a normal monster), drawn bigger, with the ground slam.
            GameObject bossPrefab = CreateMonsterPrefab(new MonsterSpec
            {
                Name = "Dungeon Boss", Animations = kingAnimations, Scale = 1.5f, Health = SourceHealth(6), MinDamage = 20, MaxDamage = 30,
                AttackInterval = 2f, Level = 6, Experience = LevelCurve.KillExperienceFor(6), MoveSpeed = 1.7f, Gold = new Vector2Int(20, 30),
                Loot = Loot(items, ("ChippedRuby", 0.05f), ("ChippedTopaz", 0.05f), ("ChippedSapphire", 0.05f), ("ChippedEmerald", 0.05f), ("RoughAmethyst", 0.05f), ("Ruby", 0.01f), ("Sapphire", 0.01f), ("HealingPotion", 0.6f), ("ManaPotion", 0.3f)),
            }, barSprite, lootBagPrefab, affixes, boss => boss.AddComponent<BossMechanics>());
            ChestPrefabs chests = CreateChestPrefabs(items, affixes, lootBagPrefab);
            DungeonDefinition[] dungeons = CreateDungeons(goblinPrefab, brutePrefab, orcPrefab, bossPrefab,
                CreateDungeonContainers(chests, items, affixes, lootBagPrefab));
            CharacterAnimationSet rangerAnimations = CreateCharacterAnimationSet("Ranger", RangerArtFolder, RangerAnimationSetPath, new Color(0.2f, 0.45f, 0.22f), null);
            CharacterAnimationSet mageAnimations = CreateCharacterAnimationSet("Mage", MageArtFolder, MageAnimationSetPath, new Color(0.45f, 0.22f, 0.62f), null);
            ClassCatalog classes = CreateClasses(playerAnimations, rangerAnimations, mageAnimations, items);
            Sprite projectileSprite = PlaceholderArtGenerator.Orb("Projectile");
            Sprite effectRingSprite = PlaceholderArtGenerator.GroundRing("EffectRing", Color.white);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Grid grid = CreateGrid();
            Tilemap ground = CreateTilemap(grid, "Ground", -100, TilemapRenderer.Mode.Chunk);
            Tilemap obstacles = CreateTilemap(grid, "Obstacles", 0, TilemapRenderer.Mode.Individual);
            obstacles.gameObject.AddComponent<TilemapCollider2D>();
            PaintTestMap(ground, obstacles, grassTiles, pathTile, rockTile, blockedTile);
            PlaceTrees(grid, treePrefab);
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
            PlaceEdgeMonsters(grid, obstacles, orcPrefab);
            PlaceDungeons(grid, obstacles, dungeons);
            VendorDefinition[] vendors = CreateVendors(items, out Vector3Int[] vendorCells);
            PlaceVendors(grid, vendors, vendorCells, affixes);
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

        /// <summary>
        /// The source game's ground (<see cref="WorldMapData"/>, from its fanadin.map): its walls, fences and rocks as rock
        /// tiles, its trees and other blocked objects as tree cells (drawn by <see cref="PlaceTrees"/>), everything else
        /// grass, with a paved square in the middle of St. Revere and at the start.
        /// </summary>
        private static void PaintTestMap(Tilemap ground, Tilemap obstacles, Tile[] grassTiles, Tile pathTile, Tile rockTile, Tile blockedTile)
        {
            var random = new System.Random(Seed);
            TreeCells.Clear();
            for (int y = 0; y < MapHeight; y++)
            {
                string row = WorldMapData.Rows[y];
                for (int x = 0; x < MapWidth; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    char kind = x < row.Length ? row[x] : WorldMapData.Wall;
                    bool square = Within(cell, TownCell, TownSquareRadius) || Within(cell, StartCell, StartSquareRadius);
                    ground.SetTile(cell, square && kind == WorldMapData.Walkable ? pathTile : grassTiles[random.Next(grassTiles.Length)]);
                    if (kind == WorldMapData.Wall) obstacles.SetTile(cell, rockTile);
                    else if (kind == WorldMapData.Tree)
                    {
                        // Blocked by an invisible tile; the tree itself is a prop on some of these cells.
                        obstacles.SetTile(cell, blockedTile);
                        TreeCells.Add(cell);
                    }
                }
            }
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
            player.AddComponent<Spellbook>();
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

        // The source game's tree cells (blocked), so nothing is put inside one.
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

        /// <summary>
        /// A tree on the source game's tree cells: one per little clump (no two closer than two cells), so a wood looks like
        /// a wood and not a wall of trunks. The cells block through their tiles; the trees' own trunks are not needed.
        /// </summary>
        private static void PlaceTrees(Grid grid, GameObject treePrefab)
        {
            var props = new GameObject("Props").transform;
            var drawn = new HashSet<Vector3Int>();
            foreach (Vector3Int cell in TreeCells.OrderBy(c => c.y).ThenBy(c => c.x))
            {
                bool crowded = false;
                for (int dx = -1; dx <= 1 && !crowded; dx++)
                for (int dy = -1; dy <= 1 && !crowded; dy++)
                    crowded = (dx != 0 || dy != 0) && drawn.Contains(new Vector3Int(cell.x + dx, cell.y + dy, 0));
                if (crowded) continue;
                drawn.Add(cell);
                var tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, props);
                tree.transform.position = grid.GetCellCenterWorld(cell);
                // The cell already blocks; a trunk collider would only make paths around it tighter.
                foreach (Collider2D trunk in tree.GetComponents<Collider2D>()) UnityEngine.Object.DestroyImmediate(trunk);
            }
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
            // Every unit can carry buffs and debuffs (stuns, slows, damage over time...).
            go.AddComponent<StatusEffects>();
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

        /// <summary>The Tomes of each class (by class index), sold by its trainer.</summary>
        private static readonly Dictionary<int, List<ItemDefinition>> ClassTomes = new Dictionary<int, List<ItemDefinition>>();

        private static ClassCatalog CreateClasses(CharacterAnimationSet warriorLook, CharacterAnimationSet rangerLook, CharacterAnimationSet mageLook,
            ItemCatalog items)
        {

            // Attributes, and health and mana at level 1, are the source game's (player_class_stats): warrior 75/30,
            // ranger 60/45, mage 40/70. Both grow with LevelCurve.HealthGrowth; attributes and gear add more.
            // Spells are the source game's too (TestSceneBuilder.Spells.cs): the Paladin's for the warrior.
            AbilityDefinition[] warriorSpells = CreateWarriorSpells();
            AbilityDefinition[] rangerSpells = CreateRangerSpells();
            AbilityDefinition[] mageSpells = CreateMageSpells();
            CharacterClassDefinition warrior = CreateClass("Warrior", warriorLook, 75, 5, 9, 1f, 1.5f, 0.9f, 0.06f, Color.clear,
                new AttributeSet(15, 10, 5, 15), RPG.Progression.Attribute.Strength, 30, warriorSpells);
            CharacterClassDefinition ranger = CreateClass("Ranger", rangerLook, 60, 3, 6, 0.8f, 1.7f, 5f, 0.09f, new Color(0.85f, 0.75f, 0.45f),
                new AttributeSet(10, 15, 10, 10), RPG.Progression.Attribute.Agility, 45, rangerSpells);
            CharacterClassDefinition mage = CreateClass("Mage", mageLook, 40, 0, 3, 0.8f, 1.8f, 4.5f, 0.08f, new Color(0.7f, 0.5f, 1f),
                new AttributeSet(5, 5, 20, 10), RPG.Progression.Attribute.Intelligence, 70, mageSpells);
            // The weapon skills each class trains: the paladin's maces, axes, swords and shields; the ranger's bows and daggers;
            // the mage's staves and wands.
            warrior.weaponSkills = new[] { RPG.Progression.Attribute.Maces, RPG.Progression.Attribute.Axes, RPG.Progression.Attribute.Swords,
                RPG.Progression.Attribute.Shields };
            ranger.weaponSkills = new[] { RPG.Progression.Attribute.Ranged, RPG.Progression.Attribute.Daggers };
            mage.weaponSkills = new[] { RPG.Progression.Attribute.Staves, RPG.Progression.Attribute.Wands };
            foreach (CharacterClassDefinition definition in new[] { warrior, ranger, mage }) EditorUtility.SetDirty(definition);

            // Their Tomes go at the end of the item catalog (players keep item ids).
            ClassTomes.Clear();
            ClassTomes[WarriorClass] = CreateTomes(warriorSpells, WarriorClass);
            ClassTomes[RangerClass] = CreateTomes(rangerSpells, RangerClass);
            ClassTomes[MageClass] = CreateTomes(mageSpells, MageClass);
            var allItems = new List<ItemDefinition>(items.items);
            foreach (List<ItemDefinition> tomes in ClassTomes.Values)
                foreach (ItemDefinition tome in tomes)
                    if (!allItems.Contains(tome)) allItems.Add(tome);
            items.items = allItems.ToArray();
            EditorUtility.SetDirty(items);

            var catalog = LoadOrCreateAsset<ClassCatalog>($"{ClassesFolder}/ClassCatalog.asset");
            // Players store the index, so keep this order.
            catalog.classes = new[] { warrior, ranger, mage };
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static CharacterClassDefinition CreateClass(string className, CharacterAnimationSet look, int baseHealth,
            int minDamage, int maxDamage, float damagePerLevel, float attackInterval, float attackRange, float critChance,
            Color projectileColor, AttributeSet attributes, RPG.Progression.Attribute primaryAttribute, int baseMana,
            AbilityDefinition[] abilities)
        {
            var definition = LoadOrCreateAsset<CharacterClassDefinition>($"{ClassesFolder}/{className}.asset");
            definition.displayName = className;
            definition.animations = look;
            definition.baseHealth = baseHealth;
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

        /// <summary>The source game's normal monster health at a level (<see cref="MonsterAI.SourceHealthAt"/>).</summary>
        private static int SourceHealth(int level) => Mathf.RoundToInt(MonsterAI.SourceHealthAt(level));

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

        /// <summary>
        /// The source game's named places of the start region, with a waystone where the source game has a waypoint (and in town).
        /// Waystone ids are saved with characters: keep them, only add new ones.
        /// </summary>
        private static void PlaceZonesAndWaystones(Grid grid, Tilemap obstacles, Sprite waystoneSprite)
        {
            // One cell step is about 0.56 world units diagonally.
            const float cellToWorld = 0.56f;
            var zones = new GameObject("Zones").transform;
            foreach ((string name, Vector3Int cell, float radius) in StartAreaData.OtherZones)
            {
                var zone = new GameObject($"Zone {name}");
                zone.transform.SetParent(zones, false);
                zone.transform.position = grid.GetCellCenterWorld(cell);
                zone.AddComponent<Zone>().Configure(name, radius * cellToWorld);
            }

            var places = StartAreaData.Places;
            var stones = new GameObject("Waystones").transform;
            for (int i = 0; i < places.Length; i++)
            {
                var zone = new GameObject($"Zone {places[i].name}");
                zone.transform.SetParent(zones, false);
                zone.transform.position = grid.GetCellCenterWorld(places[i].cell);
                zone.AddComponent<Zone>().Configure(places[i].name, places[i].zoneRadius * cellToWorld);

                // At the start the stone stands at the corner of the square, not in the middle of it.
                Vector3Int stoneCell = OpenCellNear(obstacles, places[i].cell == StartCell ? StartCell + new Vector3Int(-3, 3, 0) : places[i].cell);
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

            // The first tier of the source game's potions keeps the old files (and so the ids in saved bags).
            potion = CreateItem("HealingPotion", "Minor Life Potion", ItemSlot.None, ItemRarity.Common, 0, 0, 0, LifeColor, 10);
            manaPotion = CreateItem("ManaPotion", "Minor Mana Potion", ItemSlot.None, ItemRarity.Common, 0, 0, 0, ManaColor, 10);

            var catalog = LoadOrCreateAsset<ItemCatalog>($"{ItemsFolder}/ItemCatalog.asset");
            // Players will store item ids (catalog index + 1): only ever append to this list.
            catalog.items = new[]
            {
                potion,
                // Dropped by monsters; any class can use them.
                CreateItem("RustySword", "Rusty Sword", ItemSlot.Weapon, ItemRarity.Common, 2, 0, 0, metal * 0.8f, 8, WarriorClass, itemLevel: 1),
                CreateItem("IronSword", "Iron Sword", ItemSlot.Weapon, ItemRarity.Uncommon, 4, 0, 0, metal, 40, WarriorClass, itemLevel: 3),
                CreateItem("GoblinCleaver", "Goblin Cleaver", ItemSlot.Weapon, ItemRarity.Rare, 7, 10, 0, new Color(0.5f, 0.6f, 0.4f), 160, WarriorClass, itemLevel: 5),
                CreateItem("OrcishGreataxe", "Orcish Greataxe", ItemSlot.Weapon, ItemRarity.Epic, 12, 20, 0, orcish, 500, WarriorClass, itemLevel: 8),
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
            // The source game's gems: Rough, plain and Refined, five of a kind transmute into the next tier. The files keep
            // their old names (Chipped, Flawless) so item ids stay; Amethyst came later and is at the end of the catalog.
            var gems = new List<ItemDefinition>();
            foreach ((string gemName, RPG.Progression.Attribute attribute, Color color) in GemKinds.Take(4))
                gems.AddRange(CreateGemTiers(gemName, attribute, color, $"Chipped{gemName}", $"Flawless{gemName}"));
            var all = new List<ItemDefinition>(catalog.items);
            // The hand-made gear is only sold now: drops by level come from the source game's gear below.
            foreach (ItemDefinition item in all)
                if (item.IsEquippable) item.dropWeight = 0f;
            all.AddRange(gems);
            all.AddRange(CreateGearData(out List<ItemDefinition> materials));
            all.AddRange(materials);
            all.AddRange(CreatePotions(potion, manaPotion));
            all.AddRange(CreateDungeonItems());
            all.AddRange(CreateTownSupplies());
            (string amethyst, RPG.Progression.Attribute intelligence, Color purple) = GemKinds[4];
            all.AddRange(CreateGemTiers(amethyst, intelligence, purple, $"Rough{amethyst}", $"Refined{amethyst}"));
            all.AddRange(CreateMoreGear());
            all.AddRange(CreateSourceJunk());
            catalog.items = all.ToArray();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>The source game's gems and their stats (item_template): Ruby Strength, Topaz Courage, Sapphire Willpower,
        /// Emerald Agility, Amethyst Intelligence.</summary>
        private static readonly (string name, RPG.Progression.Attribute attribute, Color color)[] GemKinds =
        {
            ("Ruby", RPG.Progression.Attribute.Strength, new Color(0.85f, 0.1f, 0.15f)),
            ("Topaz", RPG.Progression.Attribute.Vitality, new Color(0.95f, 0.65f, 0.15f)),
            ("Sapphire", RPG.Progression.Attribute.Willpower, new Color(0.15f, 0.35f, 0.95f)),
            ("Emerald", RPG.Progression.Attribute.Agility, new Color(0.15f, 0.75f, 0.3f)),
            ("Amethyst", RPG.Progression.Attribute.Intelligence, new Color(0.6f, 0.3f, 0.85f)),
        };

        /// <summary>
        /// One gem's tiers like the source game's: Rough (green, stat 1 there), plain (blue, 2), Refined (gold, 3); here
        /// twice that (2, 4, 6) for our attribute scale. Vendors pay the source sell price (62, 250, 500) and ask six times it.
        /// </summary>
        private static List<ItemDefinition> CreateGemTiers(string gemName, RPG.Progression.Attribute attribute, Color color,
            string roughFile, string refinedFile)
        {
            // The source game's prices: sell 62, 250, 500; vendors ask six times that.
            ItemDefinition refined = CreateGem(refinedFile, $"Refined {gemName}", ItemRarity.Epic, attribute, 6, color * 1.15f, 3000, null);
            ItemDefinition normal = CreateGem(gemName, gemName, ItemRarity.Rare, attribute, 4, color, 1500, refined);
            ItemDefinition rough = CreateGem(roughFile, $"Rough {gemName}", ItemRarity.Uncommon, attribute, 2, color * 0.8f, 372, normal);
            foreach ((ItemDefinition gem, int sell) in new[] { (rough, 62), (normal, 250), (refined, 500) })
            {
                gem.combineCount = 5;
                gem.maxStack = 5;
                gem.sellValue = sell;
            }
            return new List<ItemDefinition> { rough, normal, refined };
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
            switch (GearData.KindOf(armorType))
            {
                case GearData.ArmorKind.Leather: return new Color(0.55f, 0.38f, 0.22f) * (0.9f + 0.1f * (armorType - 2));
                case GearData.ArmorKind.Chain:
                case GearData.ArmorKind.Plate: return MetalColors[armorType - 5];
                case GearData.ArmorKind.Robe:
                    return new[] { new Color(0.85f, 0.8f, 0.7f), new Color(0.6f, 0.55f, 0.5f), new Color(0.7f, 0.5f, 0.85f), new Color(0.5f, 0.8f, 0.95f) }[armorType - 12];
                default: return new Color(0.8f, 0.8f, 0.75f);
            }
        }

        /// <summary>Asset file name of a generated piece, e.g. "Generated/SteelSword_L04".</summary>
        private static string GearFile(string displayName, int level) => $"Generated/{displayName.Replace(" ", "")}_L{level:00}";

        /// <summary>What kind of armor each generated armor piece is (which class vendor crafts it); weapons are not in it.</summary>
        private static readonly Dictionary<ItemDefinition, GearData.ArmorKind> GearArmorKinds =
            new Dictionary<ItemDefinition, GearData.ArmorKind>();

        /// <summary>
        /// A former crafting material (ingots, logs, hides, cloth): gear drops finished now, as in the source game, so these no
        /// longer drop; they stay in the catalog (saved item ids) and sell to vendors.
        /// </summary>
        private static ItemDefinition CreateMaterial(string displayName, int firstLevel, Color color)
        {
            ItemRarity rarity = firstLevel < 6 ? ItemRarity.Common : firstLevel < 13 ? ItemRarity.Uncommon
                : firstLevel < 20 ? ItemRarity.Rare : ItemRarity.Epic;
            ItemDefinition material = CreateItem($"Generated/Materials/{displayName.Replace(" ", "")}", displayName, ItemSlot.None,
                rarity, 0, 0, 0, color, 2 + firstLevel);
            material.maxStack = 50;
            material.canHaveAffix = false;
            material.description = "Once used for crafting; sell it to a vendor.";
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
        /// The source game's gear (<see cref="GearData"/>): for every level, each material its chance tables can drop
        /// there, as a sword (warriors), bow (rangers) and staff (mages), and as head, chest and feet armor anyone may
        /// wear. Stats grow with the source game's sell price of the piece, the drop weight is its material chance: monsters,
        /// chests and merchants give these pieces. The old materials stay in the catalog.
        /// </summary>
        private static List<ItemDefinition> CreateGearData(out List<ItemDefinition> materials)
        {
            GearArmorKinds.Clear();
            var gear = new List<ItemDefinition>();
            materials = new List<ItemDefinition>();
            var ingots = new ItemDefinition[GearData.Metals.Length];
            var logs = new ItemDefinition[GearData.Woods.Length];
            for (int tier = 1; tier <= ingots.Length; tier++)
            {
                (int, int, float)[] weaponTable = GearData.WeaponChances;
                ingots[tier - 1] = CreateMaterial($"{GearData.Metals[tier - 1]} Ingot", FirstLevel(weaponTable, tier), MetalColors[tier - 1]);
                logs[tier - 1] = CreateMaterial($"{GearData.Woods[tier - 1]} Log", FirstLevel(weaponTable, tier), WoodColors[tier - 1]);
            }
            materials.AddRange(ingots);
            materials.AddRange(logs);
            // Armor: hides for leather, the metal ingots for chain (Bronze-Emerald) and plate (Crystal-Titanium), cloth for robes.
            var armorMaterials = new ItemDefinition[GearData.ArmorMaterials.Length];
            for (int type = 2; type < armorMaterials.Length; type++)
            {
                GearData.ArmorKind kind = GearData.KindOf(type);
                if (kind == GearData.ArmorKind.Chain || kind == GearData.ArmorKind.Plate)
                {
                    armorMaterials[type] = ingots[type - 5];
                    continue;
                }
                string name = GearData.ArmorMaterials[type] + (kind == GearData.ArmorKind.Leather ? " Hide" : " Cloth");
                armorMaterials[type] = CreateMaterial(name, FirstLevel(GearData.ArmorChances, type), ArmorColor(type));
                materials.Add(armorMaterials[type]);
            }

            ItemDefinition Piece(string displayName, ItemSlot slot, int level, float chance, int damage, int armor, int mana,
                Color color, float sourceSellPrice, int requiredClass, List<ItemDefinition> into)
            {
                // The source game's prices: vendors pay its sell price and ask three times that.
                int sell = Mathf.Max(1, Mathf.RoundToInt(sourceSellPrice));
                ItemDefinition item = CreateItem(GearFile(displayName, level), displayName, slot, ItemRarity.Common, damage, 0, armor,
                    color, sell * 3, requiredClass, level);
                item.sellValue = sell;
                item.manaBonus = mana;
                item.dropWeight = chance / 100f;
                into.Add(item);
                return item;
            }

            // Weapons: the source game's base names and sell prices (GearSourceData), damage from the price.
            foreach ((int tier, int level, float chance) in GearData.WeaponChances)
            {
                foreach ((int weaponType, int equip, int material, string fallback, int classIndex, Color color) in new[]
                         {
                             (4, 9, tier, "Sword", WarriorClass, MetalColors[tier - 1]),
                             (2, 11, 7 + tier, "Bow", RangerClass, WoodColors[tier - 1]),
                             (5, 9, 7 + tier, "Staff", MageClass, WoodColors[tier - 1] * 0.9f),
                         })
                {
                    (string baseName, float sell) = SourcePiece(equip, weaponType, material, level, fallback, GearData.WeaponSellPrice(tier, level));
                    int damage = Mathf.RoundToInt(2f + (sell - 50f) * 0.035f);
                    string materialName = weaponType == 4 ? GearData.Metals[tier - 1] : GearData.Woods[tier - 1];
                    ItemDefinition weapon = Piece($"{materialName} {baseName}", ItemSlot.Weapon, level, chance, damage, 0,
                        weaponType == 5 ? Mathf.RoundToInt(damage * 0.5f) : 0, color, sell, classIndex, gear);
                    weapon.weaponType = weaponType == 4 ? WeaponType.Sword : weaponType == 2 ? WeaponType.Ranged : WeaponType.Staff;
                }
            }

            // Armor: head, chest and feet first (their places in the catalog are saved with characters), then legs and hands
            // at the end (CreateMoreGear). Armor grows with the chest's price, shared by slot; the price is each piece's own.
            foreach ((int type, int level, float chance) in GearData.ArmorChances)
                foreach ((int equip, ItemSlot slot, float share) in new[] { (1, ItemSlot.Head, 0.7f), (3, ItemSlot.Chest, 1f), (6, ItemSlot.Feet, 0.5f) })
                    ArmorPiece(type, level, chance, equip, slot, share, gear);
            return gear;

            void ArmorPiece(int type, int level, float chance, int equip, ItemSlot slot, float share, List<ItemDefinition> into)
            {
                float power = SourcePiece(3, 0, type, level, null, GearData.ArmorSellPrice(type, level)).sell - 50f;
                bool robe = GearData.KindOf(type) == GearData.ArmorKind.Robe;
                // Robes cost like armor but give mana: little armor, a mana pool that grows with their price.
                float armor = robe ? 1f + level * 0.15f : 3f + power * 0.06f;
                float mana = robe ? 4f + power * 0.12f : 0f;
                (string head, string chest, string feet) = GearData.NamesOf(GearData.KindOf(type));
                string fallback = slot == ItemSlot.Head ? head : slot == ItemSlot.Chest ? chest : feet;
                (string baseName, float sell) = SourcePiece(equip, 0, type, level, fallback, GearData.ArmorSellPrice(type, level));
                ItemDefinition piece = Piece($"{GearData.ArmorMaterials[type]} {baseName}", slot, level, chance, 0,
                    Mathf.Max(1, Mathf.RoundToInt(armor * share)), Mathf.RoundToInt(mana * share), ArmorColor(type), sell, -1, into);
                GearArmorKinds[piece] = GearData.KindOf(type);
                piece.classMask = ArmorClasses(GearData.KindOf(type));
            }
        }

        /// <summary>
        /// The source game's other equipment slots, at the end of the catalog: legs and hands of every armor type, a
        /// necklace, belt and ring for every level (green or better: they always roll an affix), and the warrior's shield
        /// for every level (armor from its price). Names and prices are the source game's plain pieces.
        /// </summary>
        private static List<ItemDefinition> CreateMoreGear()
        {
            var gear = new List<ItemDefinition>();
            foreach ((int type, int level, float chance) in GearData.ArmorChances)
            {
                float power = SourcePiece(3, 0, type, level, null, GearData.ArmorSellPrice(type, level)).sell - 50f;
                bool robe = GearData.KindOf(type) == GearData.ArmorKind.Robe;
                float armor = robe ? 1f + level * 0.15f : 3f + power * 0.06f;
                float mana = robe ? 4f + power * 0.12f : 0f;
                foreach ((int equip, ItemSlot slot, float share) in new[] { (5, ItemSlot.Legs, 0.8f), (7, ItemSlot.Hands, 0.5f) })
                {
                    (string baseName, float sell) = SourcePiece(equip, 0, type, level, slot.ToString(), GearData.ArmorSellPrice(type, level));
                    string name = $"{GearData.ArmorMaterials[type]} {baseName}";
                    int price = Mathf.Max(1, Mathf.RoundToInt(sell));
                    ItemDefinition piece = CreateItem(GearFile(name, level), name, slot, ItemRarity.Common, 0, 0,
                        Mathf.Max(1, Mathf.RoundToInt(armor * share)), ArmorColor(type), price * 3, -1, level);
                    piece.sellValue = price;
                    piece.manaBonus = Mathf.RoundToInt(mana * share);
                    piece.dropWeight = chance / 100f;
                    GearArmorKinds[piece] = GearData.KindOf(type);
                    piece.classMask = ArmorClasses(GearData.KindOf(type));
                    gear.Add(piece);
                }
            }

            for (int level = 1; level <= 25; level++)
            {
                foreach ((int equip, ItemSlot slot, string fallback, Color color) in new[]
                         {
                             (2, ItemSlot.Neck, "Pendant", new Color(0.95f, 0.85f, 0.45f)),
                             (4, ItemSlot.Waist, "Girdle", new Color(0.6f, 0.45f, 0.3f)),
                             (8, ItemSlot.Ring, "Ring", new Color(0.9f, 0.8f, 0.35f)),
                             (10, ItemSlot.Shield, "Barricade", new Color(0.55f, 0.45f, 0.35f)),
                         })
                {
                    (string name, float sell) = SourcePiece(equip, 0, 0, level, fallback, 50f + 10f * level);
                    int price = Mathf.Max(1, Mathf.RoundToInt(sell));
                    bool shield = slot == ItemSlot.Shield;
                    ItemDefinition piece = CreateItem(GearFile(name, level), name, slot, ItemRarity.Common, 0, 0,
                        shield ? Mathf.Max(1, Mathf.RoundToInt(3f + (sell - 50f) * 0.06f)) : 0, color, price * 3,
                        shield ? WarriorClass : -1, level);
                    piece.sellValue = price;
                    // Jewelry is worth its affix: it always has one. About as common as one armor material of the level.
                    piece.alwaysAffix = !shield;
                    piece.dropWeight = 0.5f;
                    if (shield) piece.weaponType = WeaponType.Shield;
                    gear.Add(piece);
                }
            }
            return gear;
        }

        /// <summary>The source game's junk (MonsterData.Junk) by kind and item level 1-25.</summary>
        private static readonly Dictionary<(int kind, int level), ItemDefinition> SourceJunk = new Dictionary<(int, int), ItemDefinition>();

        /// <summary>
        /// The source game's junk loot (npc_models_junkloot): every kind ("Hoof", "Insect Scale") for item levels 1-25, sold
        /// for the source price. At the end of the catalog.
        /// </summary>
        private static List<ItemDefinition> CreateSourceJunk()
        {
            SourceJunk.Clear();
            var created = new List<ItemDefinition>();
            for (int kind = 0; kind < MonsterData.Junk.Length; kind++)
            {
                (string kindName, (string name, int sell, int stack)[] levels) = MonsterData.Junk[kind];
                for (int level = 1; level <= levels.Length; level++)
                {
                    (string name, int sell, int stack) = levels[level - 1];
                    ItemDefinition item = CreateJunk($"Junk/{name.Replace(" ", "")}", name, new Color(0.6f, 0.55f, 0.45f), Mathf.Max(1, sell) * 4);
                    item.sellValue = Mathf.Max(1, sell);
                    item.maxStack = Mathf.Max(1, stack);
                    item.itemLevel = level;
                    SourceJunk[(kind, level)] = item;
                    created.Add(item);
                }
            }
            return created;
        }

        /// <summary>Chance a monster drops a piece of junk (the source game rolls it on its server).</summary>
        private const float JunkChance = 0.6f;

        /// <summary>
        /// The junk a monster of the source game drops (by its name, MonsterData.JunkByMonster), as loot entries at its
        /// level: one of its kinds at <see cref="JunkChance"/>. Empty for monsters without junk.
        /// </summary>
        private static LootEntry[] JunkLoot(string monsterName, int level)
        {
            int[] kinds = null;
            foreach ((string monster, int[] junk) in MonsterData.JunkByMonster)
                if (monster == monsterName) kinds = junk;
            if (kinds == null || kinds.Length == 0) return Array.Empty<LootEntry>();
            int itemLevel = Mathf.Clamp(level, 1, 25);
            return kinds.Where(kind => SourceJunk.ContainsKey((kind, itemLevel)))
                .Select(kind => new LootEntry(SourceJunk[(kind, itemLevel)], JunkChance / kinds.Length)).ToArray();
        }

        /// <summary>
        /// Who may wear an armor kind, from the source game's class armor (player_desirable_armor): the paladin (our warrior)
        /// leather, chain and plate; the ranger leather and chain; the mage cloth and robes.
        /// </summary>
        private static int ArmorClasses(GearData.ArmorKind kind)
        {
            const int warrior = 1 << WarriorClass, ranger = 1 << RangerClass, mage = 1 << MageClass;
            switch (kind)
            {
                case GearData.ArmorKind.Leather:
                case GearData.ArmorKind.Chain: return warrior | ranger;
                case GearData.ArmorKind.Plate: return warrior;
                case GearData.ArmorKind.Robe:
                case GearData.ArmorKind.Cloth: return mage;
                default: return 0;
            }
        }

        /// <summary>The source game's base name and sell price of a plain piece (GearSourceData), or the fallbacks.</summary>
        private static (string name, float sell) SourcePiece(int equip, int weaponType, int key, int level, string fallbackName, float fallbackSell)
        {
            foreach (var piece in GearSourceData.Pieces)
                if (piece.equip == equip && piece.weapon == weaponType && piece.key == key && piece.level == level)
                    return (piece.name, piece.sell);
            return (fallbackName, fallbackSell);
        }

        private static readonly Color LifeColor = new Color(0.85f, 0.15f, 0.2f);
        private static readonly Color ManaColor = new Color(0.2f, 0.35f, 0.95f);

        /// <summary>
        /// The source game's potions (item_template and their spells): Minor, Lesser, plain, Greater and Major Life and Mana
        /// Potions (levels 1, 5, 12, 18, 24) restore over 20 seconds; their Serums (green) restore at once. Amounts are
        /// the source game's (a Minor Life Potion 9 every 2 s, 90 in all; a Major Life Serum 600). Five of a kind combine into the next tier, a Major Potion into its
        /// Serum (the source game's crafting recipes). Fills in the given Minor potions and returns the new ones.
        /// </summary>
        private static List<ItemDefinition> CreatePotions(ItemDefinition minorLife, ItemDefinition minorMana)
        {
            string[] tiers = { "Minor", "Lesser", "", "Greater", "Major" };
            int[] levels = { 1, 5, 12, 18, 24 };
            int[] lifePotion = { 90, 140, 200, 300, 400 };
            int[] lifeSerum = { 135, 217, 300, 450, 600 };
            int[] manaPotion = { 110, 180, 250, 370, 500 };
            int[] manaSerum = { 162, 271, 375, 540, 720 };
            // The source game's sell prices; vendors ask three times that (its buy cost ratio).
            int[] potionSell = { 5, 25, 60, 90, 120 };
            int[] serumSell = { 7, 37, 90, 135, 180 };
            var created = new List<ItemDefinition>();

            void Chain(string kind, ItemUse use, int[] potionAmounts, int[] serumAmounts, Color color, ItemDefinition minor)
            {
                ItemDefinition previousPotion = null, previousSerum = null;
                for (int tier = 0; tier < tiers.Length; tier++)
                {
                    string prefix = tiers[tier].Length > 0 ? tiers[tier] + " " : "";
                    ItemDefinition potion = tier == 0 ? minor
                        : CreateItem($"{tiers[tier]}{kind}Potion", $"{prefix}{kind} Potion", ItemSlot.None, ItemRarity.Common, 0, 0, 0,
                            color * (1f + tier * 0.06f), potionSell[tier] * 3);
                    ItemDefinition serum = CreateItem($"{tiers[tier]}{kind}Serum", $"{prefix}{kind} Serum", ItemSlot.None, ItemRarity.Uncommon,
                        0, 0, 0, Color.Lerp(color, Color.white, 0.3f), serumSell[tier] * 3);
                    potion.price = potionSell[tier] * 3;
                    potion.sellValue = potionSell[tier];
                    serum.sellValue = serumSell[tier];
                    foreach ((ItemDefinition item, int amount, float seconds) in new[] { (potion, potionAmounts[tier], 20f), (serum, serumAmounts[tier], 0f) })
                    {
                        item.use = use;
                        item.restoreAmount = amount;
                        item.restoreSeconds = seconds;
                        item.itemLevel = levels[tier];
                        item.maxStack = 10;
                        item.canHaveAffix = false;
                        item.combineCount = 5;
                    }
                    potion.strongerVariant = serum;
                    if (previousPotion != null) previousPotion.combinesInto = potion;
                    if (previousSerum != null) previousSerum.combinesInto = serum;
                    previousPotion = potion;
                    previousSerum = serum;
                    if (tier > 0) created.Add(potion);
                    created.Add(serum);
                }
                // Five Major Potions make a Major Serum.
                previousPotion.combinesInto = previousSerum;
            }

            Chain("Life", ItemUse.Heal, lifePotion, lifeSerum, LifeColor, minorLife);
            Chain("Mana", ItemUse.RestoreMana, manaPotion, manaSerum, ManaColor, minorMana);
            return created;
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
            // The weapon skill that raises a weapon's damage, from its name.
            string lowerName = fileName.ToLowerInvariant();
            item.weaponType = slot != ItemSlot.Weapon ? WeaponType.None
                : lowerName.Contains("bow") ? WeaponType.Ranged
                : lowerName.Contains("staff") ? WeaponType.Staff
                : lowerName.Contains("axe") || lowerName.Contains("cleaver") ? WeaponType.Axe
                : WeaponType.Sword;
            item.requiredClass = requiredClass;
            item.itemLevel = itemLevel;
            item.manaBonus = 0;
            item.attributes = default;
            item.canHaveAffix = true;
            item.isGem = false;
            item.combinesInto = null;
            item.combineCount = 3;
            item.dropWeight = 1f;
            item.healFraction = 0.35f;
            item.restoreAmount = 0;
            item.restoreSeconds = 0f;
            item.strongerVariant = null;
            item.description = null;
            item.combineWith = Array.Empty<ItemDefinition>();
            item.combineResult = null;
            item.castSeconds = 0f;
            item.useCooldown = 0f;
            item.alwaysAffix = false;
            item.classMask = 0;
            item.sellValue = 0;
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
                AttributeSet attributes, int damage = 0, int health = 0, int armor = 0, int mana = 0) =>
                Fill(LoadOrCreateAsset<AffixDefinition>($"{AffixesFolder}/{fileName}.asset"), displayName, isPrefix, quality,
                    minLevel, maxLevel, attributes, damage, health, armor, mana);

            AffixDefinition Fill(AffixDefinition affix, string displayName, bool isPrefix, ItemRarity quality, int minLevel, int maxLevel,
                AttributeSet attributes, int damage, int health, int armor, int mana)
            {
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
                affix.retired = false;
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
            // Our first affixes stay for items that have them; only the source game's roll now.
            foreach (AffixDefinition old in catalog.affixes) old.retired = true;

            // The source game's affixes, all 7145 (all suffixes: "Steel Sword of the Novice", "of Magical Vim"), kept inside the
            // catalog asset. Its values are multipliers (0.3-3.0) of a level-based amount the server works out; here a
            // stat gets multiplier x (level + 2) / 2 at the middle of the affix's levels (Novice, levels 1-5: +5),
            // health and mana three times that. More stats make it rarer: 1 green, 2 blue, 3 gold, 4+ or level 25 purple.
            string catalogPath = AssetDatabase.GetAssetPath(catalog);
            var existing = new Dictionary<string, AffixDefinition>();
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(catalogPath))
                if (asset is AffixDefinition sub && AssetDatabase.IsSubAsset(sub)) existing[sub.name] = sub;

            var all = new List<AffixDefinition>(catalog.affixes);
            for (int i = 0; i < AffixData.All.Length; i++)
            {
                (string name, bool singleNoun, int minLevel, int maxLevel, string stats) = AffixData.All[i];
                float middle = (minLevel + maxLevel) / 2f;
                var attributes = new AttributeSet();
                int health = 0, mana = 0, armor = 0, damage = 0, count = 0;
                foreach (string stat in stats.Split(','))
                {
                    string[] pair = stat.Split(':');
                    int type = int.Parse(pair[0]);
                    float multiplier = float.Parse(pair[1], System.Globalization.CultureInfo.InvariantCulture);
                    int points = Mathf.Max(1, Mathf.RoundToInt(multiplier * (middle + 2f) / 2f));
                    count++;
                    RPG.Progression.Attribute? attribute = AffixData.AttributeOf(type);
                    if (attribute.HasValue) attributes[attribute.Value] += points;
                    else if (type == AffixData.Mana) mana += points * 3;
                    else if (type == AffixData.Health) health += points * 3;
                    else if (type == AffixData.Armor) armor += points;
                    else damage += points;
                }
                ItemRarity quality = minLevel >= 25 || count >= 4 ? ItemRarity.Legendary
                    : count == 3 ? ItemRarity.Epic : count == 2 ? ItemRarity.Rare : ItemRarity.Uncommon;

                string key = $"Affix{i:0000}";
                if (!existing.TryGetValue(key, out AffixDefinition affix))
                {
                    affix = ScriptableObject.CreateInstance<AffixDefinition>();
                    affix.name = key;
                    AssetDatabase.AddObjectToAsset(affix, catalog);
                }
                all.Add(Fill(affix, singleNoun ? $"of the {name}" : $"of {name}", false, quality, minLevel, maxLevel,
                    attributes, damage, health, armor, mana));
            }
            catalog.affixes = all.ToArray();
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
        /// One monster on every spot the source game has one between the start and St. Revere, coming back as fast as there.
        /// The source game's levels 1-2 look like goblins, 3-5 brutes, higher orcs, and keep the source-game level (health, damage,
        /// experience and gear drops follow it); its elites and bosses are elites here. None inside the towns.
        /// </summary>
        private static void PlaceMonsters(Grid grid, Tilemap obstacles, GameObject goblin, GameObject brute, GameObject orc)
        {
            var parent = new GameObject("Monster Spots").transform;
            var monsters = StartAreaData.Monsters;
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
                // The source game's name, and its junk; horned beasts and Lesser Wyrms sometimes drop Keys to the Barracks.
                string sourceName = i < MonsterData.SpotNames.Length ? MonsterData.SpotNames[i] : null;
                if (!string.IsNullOrEmpty(sourceName)) spawner.DisplayName = sourceName;
                int keyLoot = KeyDropperLoot(cell);
                spawner.BonusLoot = JunkLoot(sourceName, level).Concat(keyLoot > 0 ? DungeonLoot(keyLoot) : Array.Empty<LootEntry>()).ToArray();
                // From level 3 every fourth spot (and every elite) hits with magic: frost, fire, shadow or holy,
                // so the resists matter.
                if (level >= 3 && (elite || i % 4 == 0)) spawner.School = (DamageSchool)(1 + i / 4 % 4);
            }
        }

        private struct ChestPrefabs
        {
            public GameObject Crate;
            public GameObject Chest;
            public GameObject Strongbox;
        }

        /// <summary>
        /// The source game's containers (gameobject_template): crates and barrels hold only gold; chests ("Aspen Chest" and
        /// the like) are locked for Lockpicking of twice their level and hold a piece of gear (green 100%, blue 12%,
        /// gold 5%, purple 1%), a potion (75%), a gem (75%) and twice the gold; strongboxes two more pieces, a potion
        /// and a gem for sure and three times the gold.
        /// </summary>
        private static ChestPrefabs CreateChestPrefabs(ItemCatalog items, AffixCatalog affixes, GameObject lootBagPrefab)
        {
            var wood = new Color(0.55f, 0.38f, 0.22f);
            var darkWood = new Color(0.42f, 0.26f, 0.14f);
            var iron = new Color(0.45f, 0.47f, 0.52f);
            var gold = new Color(0.95f, 0.78f, 0.3f);
            ItemDefinition[] potions = Loot(items, ("HealingPotion", 1f), ("ManaPotion", 1f)).Select(entry => entry.item).ToArray();
            ItemDefinition[] gems = Loot(items, ("ChippedRuby", 1f), ("ChippedTopaz", 1f), ("ChippedSapphire", 1f), ("ChippedEmerald", 1f), ("RoughAmethyst", 1f))
                .Select(entry => entry.item).ToArray();

            GameObject chest = CreateChestPrefab("OakChest", "Oak Chest", 4, 2f,
                PlaceholderArtGenerator.Container("OakChest", darkWood, iron, gold, 60f, 32f, true), lootBagPrefab, affixes, items,
                lootChest => lootChest.ConfigureLocked(100f, 12f, 5f, 1f, 0, 75f, 75f, potions, gems));
            GameObject strongbox = CreateChestPrefab("IronStrongbox", "Iron Strongbox", 6, 3f,
                PlaceholderArtGenerator.Container("IronStrongbox", iron, new Color(0.3f, 0.31f, 0.35f), gold, 56f, 36f, false), lootBagPrefab, affixes,
                items, lootChest => lootChest.ConfigureLocked(100f, 15f, 5f, 1f, 2, 100f, 100f, potions, gems));
            return new ChestPrefabs
            {
                Crate = CreateChestPrefab("WoodenCrate", "Wooden Crate", 2, 1f,
                    PlaceholderArtGenerator.Container("WoodenCrate", wood, darkWood, darkWood, 52f, 34f, false), lootBagPrefab, affixes, items, null),
                Chest = chest,
                Strongbox = strongbox,
            };
        }

        private static GameObject CreateChestPrefab(string fileName, string chestName, int level, float goldRatio, Sprite sprite,
            GameObject lootBagPrefab, AffixCatalog affixes, ItemCatalog gear, Action<LootChest> configure)
        {
            var chest = new GameObject(chestName);
            var spriteRenderer = chest.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.spriteSortPoint = SpriteSortPoint.Pivot;
            chest.AddComponent<NetworkObject>();
            var lootChest = chest.AddComponent<LootChest>();
            lootChest.Configure(chestName, level, lootBagPrefab, affixes, gear, goldRatio);
            configure?.Invoke(lootChest);
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
        /// A container on every spot the source game has one in its start region, back after the same time
        /// (crates and barrels 60 s, chests 5 min). Crates and barrels become wooden crates, chests locked oak chests.
        /// </summary>
        private static void PlaceChests(Grid grid, Tilemap obstacles, ChestPrefabs chests)
        {
            var parent = new GameObject("Chests").transform;
            var spots = StartAreaData.Chests;
            for (int i = 0; i < spots.Length; i++)
            {
                (Vector3Int cell, int kind, int level, float respawn) = spots[i];
                // The source game's start region has crates, barrels and chests (its strongboxes are further on).
                GameObject prefab = kind == 0 ? chests.Crate : chests.Chest;
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
            CreateHudElement<AbilityBar>(canvasObject.transform, "Ability Bar", new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 32f), new Vector2(780f, 80f));
            var nameplates = new GameObject("NPC Nameplates", typeof(RectTransform));
            nameplates.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)nameplates.transform);
            nameplates.AddComponent<NpcNameplates>();

            var vendorWindow = new GameObject("Vendor Window", typeof(RectTransform));
            vendorWindow.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)vendorWindow.transform);
            vendorWindow.AddComponent<VendorWindow>();

            var transmuteWindow = new GameObject("Transmute Window", typeof(RectTransform));
            transmuteWindow.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)transmuteWindow.transform);
            transmuteWindow.AddComponent<TransmuteWindow>();

            var lootWindow = new GameObject("Loot Window", typeof(RectTransform));
            lootWindow.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)lootWindow.transform);
            lootWindow.AddComponent<LootWindow>();

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

            var spellbookWindow = new GameObject("Spellbook Window", typeof(RectTransform));
            spellbookWindow.transform.SetParent(canvasObject.transform, false);
            Stretch((RectTransform)spellbookWindow.transform);
            spellbookWindow.AddComponent<SpellbookWindow>();
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
