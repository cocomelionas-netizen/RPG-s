using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RPG.Characters;
using RPG.Combat;
using RPG.Dungeons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace RPG.EditorTools
{
    /// <summary>
    /// The art taken from Flare (flare-game, CC-BY-SA 3.0; Assets/_Project/Art/Flare/CREDITS.md), packed into atlases by
    /// Tools/Art/flare.py (<see cref="FlareArtData"/>): the ground, paths, water, rocks, plants and trees as tiles, and the
    /// heroes and monsters in eight directions. Sprites are cut from the atlases here and kept inside the tile and
    /// animation set assets that use them.
    /// </summary>
    public static partial class TestSceneBuilder
    {
        private const string FlareCharactersFolder = FlareArtData.Folder + "/Characters";
        private const string FlareAnimationsFolder = "Assets/_Project/Data/Characters/Flare";

        /// <summary>The Flare tiles of the world, by kind.</summary>
        private sealed class WorldTiles
        {
            public Tile[] Grass;
            public Tile[] Path;
            /// <summary>The lakes' water by the land around a cell (Tools/Art/water.py): index = land edges + land corners.</summary>
            public Tile[] Water;
            /// <summary>Rock pillars: the walls, fences and rocks of the source map.</summary>
            public Tile[] Walls;
            /// <summary>Boulders, for dungeon walls.</summary>
            public Tile[] Boulders;
            public Tile[] Trees;
            /// <summary>Bushes, ferns and tall grass (they block like the wood they stand in).</summary>
            public Tile[] Plants;
            /// <summary>Grass tufts and flowers on open ground (no collider).</summary>
            public Tile[] Decor;
            /// <summary>Buildings and props by art key (BuildingArtData; a prop's variants are key_0, key_1...): no collider.</summary>
            public Dictionary<string, Tile[]> Props;
            /// <summary>How wall cells are drawn, by PropData style (fences, iron fences, rubble): blocking.</summary>
            public Dictionary<char, Tile[]> WallStyles;
            /// <summary>Containers from Flare's dungeon tiles (their sprites draw the chests): crate, barrel, chest, strongbox.</summary>
            public Tile[] Containers;
        }

        /// <summary>
        /// The UI frames (Kenney's Fantasy UI Borders, CC0, in Resources/UI): sprites with nine-slice borders, loaded by
        /// UIFactory at runtime. The window frame keeps 16 pixel corners, the button shape 8.
        /// </summary>
        /// <summary>
        /// The painted island (Resources/UI/Map/World.png, Tools/Art/worldmap.py) for the world map and the minimap: a
        /// plain texture with mipmaps (it is drawn small on the world map and large on the minimap).
        /// </summary>
        private static Texture2D ImportWorldMapPicture()
        {
            const string path = "Assets/_Project/Resources/UI/Map/World.png";
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogWarning($"[TestScene] Missing world map picture {path}.");
                return null;
            }
            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.maxTextureSize = 2048;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ImportUiFrames()
        {
            var frames = new List<(string file, float pixelsPerUnit, int border)> { ("WindowFrame", 100f, 16), ("Button", 200f, 16), ("Divider", 100f, 0) };
            // "RPG UI 1" (CC0, Tools/Art/ui.py): the windows drawn a little finer than their pixels so their steel frame
            // is about 13 units wide; the square buttons, slots and the bar fill are not nine-sliced.
            frames.AddRange(new[]
            {
                ("Rpg/Window", 160f, 26), ("Rpg/Parchment", 160f, 26), ("Rpg/Tooltip", 120f, 10), ("Rpg/Button", 100f, 8),
                ("Rpg/ButtonHover", 100f, 8), ("Rpg/BarFrame", 200f, 12), ("Rpg/BarFill", 100f, 0),
            });
            foreach (string square in new[] { "Close", "Left", "Right", "Plus", "Minus" })
            {
                frames.Add(($"Rpg/{square}", 100f, 0));
                frames.Add(($"Rpg/{square}Hover", 100f, 0));
            }
            // The world map's badges (Tools/Art/worldmap.py).
            foreach (string badge in new[]
            {
                "Town", "Dungeon", "Boss", "Waystone", "Weapons", "Armor", "Potions", "Gems", "Food", "Bank", "Transmute", "Trainer",
                "Healer", "Goods", "Person", "Place", "Player", "Pin",
            })
                frames.Add(($"Map/{badge}", 100f, 0));
            foreach (string slot in new[] { "", "Frame", "Head", "Chest", "Shoulders", "Neck", "Hands", "Wrist", "Feet", "Legs", "Weapon", "Shield" })
                frames.Add(($"Rpg/Slot{slot}", 100f, 0));
            foreach ((string file, float pixelsPerUnit, int border) in frames)
            {
                string path = $"Assets/_Project/Resources/UI/{file}.png";
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                if (importer == null)
                {
                    Debug.LogWarning($"[TestScene] Missing UI frame {path}.");
                    continue;
                }
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.spritePixelsPerUnit = pixelsPerUnit;
                importer.spriteBorder = new Vector4(border, border, border, border);
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.filterMode = FilterMode.Bilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                importer.SaveAndReimport();
            }
        }

        /// <summary>
        /// Imports an atlas of Flare's (or the icons'): block-compressed at high quality. Not crunched: crunching made the
        /// download smaller but left blocky artifacts on the heroes and monsters.
        /// </summary>
        private static Texture2D FlareAtlas(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                throw new InvalidOperationException($"[TestScene] Missing art {path} (run Tools/Art/flare.py).");
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.crunchedCompression = false;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Removes the sprites a previous build kept inside <paramref name="owner"/>.</summary>
        private static void RemoveSubSprites(UnityEngine.Object owner)
        {
            string path = AssetDatabase.GetAssetPath(owner);
            foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetRepresentationsAtPath(path))
                if (sub is Sprite) UnityEngine.Object.DestroyImmediate(sub, true);
        }

        /// <summary>A sprite cut from an atlas, kept inside <paramref name="owner"/>.</summary>
        private static Sprite AtlasSprite(UnityEngine.Object owner, Texture2D atlas, string name, Rect rect, Vector2 pivot, float pixelsPerUnit)
        {
            Sprite sprite = Sprite.Create(atlas, rect, pivot, pixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = name;
            AssetDatabase.AddObjectToAsset(sprite, owner);
            return sprite;
        }

        private static WorldTiles CreateWorldTiles()
        {
            Texture2D atlas = FlareAtlas($"{FlareArtData.Folder}/Tiles.png");

            Tile[] Kind(string kind, Tile.ColliderType collider, Func<int, bool> pick = null)
            {
                var tiles = new List<Tile>();
                foreach (var entry in FlareArtData.Tiles)
                {
                    if (!entry.name.StartsWith(kind) || !int.TryParse(entry.name.Substring(kind.Length), out int index)) continue;
                    if (pick != null && !pick(index)) continue;
                    string tileName = collider == Tile.ColliderType.None ? entry.name : $"{entry.name}_Blocked";
                    string path = $"{TilesFolder}/Flare_{tileName}.asset";
                    var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                    if (tile == null)
                    {
                        System.IO.Directory.CreateDirectory(TilesFolder);
                        tile = ScriptableObject.CreateInstance<Tile>();
                        AssetDatabase.CreateAsset(tile, path);
                    }
                    RemoveSubSprites(tile);
                    tile.sprite = AtlasSprite(tile, atlas, entry.name, new Rect(entry.x, entry.y, entry.w, entry.h),
                        new Vector2(entry.pivotX, entry.pivotY), FlareArtData.TilePixelsPerUnit);
                    tile.colliderType = collider;
                    EditorUtility.SetDirty(tile);
                    tiles.Add(tile);
                }
                return tiles.ToArray();
            }

            return new WorldTiles
            {
                Grass = Kind("Grass", Tile.ColliderType.None),
                Path = Kind("Path", Tile.ColliderType.None),
                Water = CreateWaterTiles(),
                // Rock0-3 are boulders, Rock4-7 tall pillars.
                Walls = Kind("Rock", Tile.ColliderType.Grid, i => i >= 4),
                Boulders = Kind("Rock", Tile.ColliderType.Grid, i => i < 4),
                Trees = Kind("Tree", Tile.ColliderType.Grid),
                Plants = Kind("Plant", Tile.ColliderType.Grid),
                // Plant6-13: reeds and tall grass, drawn on open ground without blocking.
                Decor = Kind("Plant", Tile.ColliderType.None, i => i >= 6),
                Containers = Kind("Prop", Tile.ColliderType.None),
                Props = CreateBuildingTiles(Tile.ColliderType.None, "Buildings"),
                WallStyles = WallStyleTiles(CreateBuildingTiles(Tile.ColliderType.Grid, "WallParts")),
            };
        }

        /// <summary>
        /// The island's buildings and props (Tools/Art/buildings.py, BuildingArtData): one tile per sprite, its pivot the spot
        /// that stands on its cell, grouped by art key (a prop's variants key_0, key_1... under key).
        /// </summary>
        private static Dictionary<string, Tile[]> CreateBuildingTiles(Tile.ColliderType collider, string folderName)
        {
            var atlases = new Dictionary<int, Texture2D>();
            string folder = $"{TilesFolder}/{folderName}";
            System.IO.Directory.CreateDirectory(folder);
            var groups = new Dictionary<string, List<Tile>>();
            foreach (var entry in BuildingArtData.Sprites)
            {
                if (!atlases.TryGetValue(entry.page, out Texture2D atlas))
                    atlases[entry.page] = atlas = FlareAtlas($"{BuildingArtData.Folder}/Buildings{entry.page}.png");
                string path = $"{folder}/{entry.name}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, path);
                }
                RemoveSubSprites(tile);
                tile.sprite = AtlasSprite(tile, atlas, entry.name, new Rect(entry.x, entry.y, entry.w, entry.h),
                    new Vector2(entry.pivotX, entry.pivotY), entry.pixelsPerUnit);
                tile.colliderType = collider;
                // Mirrored placements set their own transform.
                tile.flags = TileFlags.None;
                EditorUtility.SetDirty(tile);
                Match variant = Regex.Match(entry.name, @"^(.*)_(\d)$");
                string key = variant.Success && !BuildingKeys.Contains(entry.name) ? variant.Groups[1].Value : entry.name;
                if (!groups.TryGetValue(key, out List<Tile> list)) groups[key] = list = new List<Tile>();
                list.Add(tile);
            }
            var result = new Dictionary<string, Tile[]>();
            foreach (var group in groups) result[group.Key] = group.Value.ToArray();
            return result;
        }

        /// <summary>The building keys of the map (their names may end in _0.._9 without being a prop's variants).</summary>
        private static readonly HashSet<string> BuildingKeys = new HashSet<string>(
            PropData.Placements.Where(p => p.building).Select(p => p.key));

        /// <summary>The blocking tiles of each wall style (PropData.WallStyles).</summary>
        private static Dictionary<char, Tile[]> WallStyleTiles(Dictionary<string, Tile[]> parts)
        {
            Tile[] Of(string key) => parts.TryGetValue(key, out Tile[] tiles) ? tiles : Array.Empty<Tile>();
            return new Dictionary<char, Tile[]>
            {
                [PropData.FenceX] = Of("fence_x"), [PropData.FenceY] = Of("fence_y"),
                [PropData.IronX] = Of("iron_x"), [PropData.IronY] = Of("iron_y"), [PropData.Rubble] = Of("rubble"),
            };
        }

        private const string WaterAtlas = "Assets/_Project/Art/Water/Water.png";

        /// <summary>
        /// The lakes' 256 water tiles (Tools/Art/water.py): water with banks of sand and foam toward the land around it,
        /// drawn over the grass. Tile index = land edges (1 x + 1, 2 y + 1, 4 x - 1, 8 y - 1) + land corners whose two
        /// edges are water (16 x + 1 y + 1, 32 x - 1 y + 1, 64 x - 1 y - 1, 128 x + 1 y - 1).
        /// </summary>
        private static Tile[] CreateWaterTiles()
        {
            Texture2D atlas = FlareAtlas(WaterAtlas);
            string folder = $"{TilesFolder}/Water";
            System.IO.Directory.CreateDirectory(folder);
            var tiles = new Tile[256];
            for (int mask = 0; mask < tiles.Length; mask++)
            {
                string path = $"{folder}/Water_{mask}.asset";
                var tile = AssetDatabase.LoadAssetAtPath<Tile>(path);
                if (tile == null)
                {
                    tile = ScriptableObject.CreateInstance<Tile>();
                    AssetDatabase.CreateAsset(tile, path);
                }
                RemoveSubSprites(tile);
                // Tile 0 at the bottom left of the atlas, 16 to a row.
                var rect = new Rect(mask % 16 * 128f, mask / 16 * 64f, 128f, 64f);
                tile.sprite = AtlasSprite(tile, atlas, $"Water_{mask}", rect, new Vector2(0.5f, 0.5f), FlareArtData.TilePixelsPerUnit);
                tile.colliderType = Tile.ColliderType.None;
                EditorUtility.SetDirty(tile);
                tiles[mask] = tile;
            }
            return tiles;
        }

        /// <summary>The water tile of a lake cell: which of its neighbours are land (see <see cref="CreateWaterTiles"/>).</summary>
        private static int WaterMask(Func<int, int, bool> isWater, int x, int y)
        {
            bool ne = !isWater(x + 1, y), nw = !isWater(x, y + 1), sw = !isWater(x - 1, y), se = !isWater(x, y - 1);
            int mask = (ne ? 1 : 0) | (nw ? 2 : 0) | (sw ? 4 : 0) | (se ? 8 : 0);
            if (!ne && !nw && !isWater(x + 1, y + 1)) mask |= 16;
            if (!nw && !sw && !isWater(x - 1, y + 1)) mask |= 32;
            if (!sw && !se && !isWater(x - 1, y - 1)) mask |= 64;
            if (!se && !ne && !isWater(x + 1, y - 1)) mask |= 128;
            return mask;
        }

        /// <summary>An animation set from a Flare look (<see cref="FlareArtData.Looks"/>): Idle, Walk and Attack in eight directions.</summary>
        private static CharacterAnimationSet FlareAnimationSet(string lookName)
        {
            // Made once per build: making it again would delete the sprites prefabs made earlier already point at.
            if (FlareSets.TryGetValue(lookName, out CharacterAnimationSet made) && made != null) return made;
            FlareArtData.Look look = FlareArtData.Looks.FirstOrDefault(l => l.Name == lookName);
            if (look == null) throw new ArgumentException($"No Flare look {lookName}.");
            Texture2D atlas = FlareAtlas($"{FlareCharactersFolder}/{lookName}.png");

            string path = $"{FlareAnimationsFolder}/{lookName}.asset";
            System.IO.Directory.CreateDirectory(FlareAnimationsFolder);
            var set = AssetDatabase.LoadAssetAtPath<CharacterAnimationSet>(path);
            if (set == null)
            {
                set = ScriptableObject.CreateInstance<CharacterAnimationSet>();
                AssetDatabase.CreateAsset(set, path);
            }
            RemoveSubSprites(set);

            int count = look.Rects.Length / 6;
            var sprites = new Sprite[count];
            for (int i = 0; i < count; i++)
            {
                int[] r = look.Rects;
                int at = i * 6;
                sprites[i] = AtlasSprite(set, atlas, $"{lookName}_{i}", new Rect(r[at], r[at + 1], r[at + 2], r[at + 3]),
                    new Vector2(r[at + 4] / 1000f, r[at + 5] / 1000f), look.PixelsPerUnit);
            }

            set.MirrorWestFromEast = false;
            set.Clips = look.Clips.Select(clip =>
            {
                Sprite[] Frames(int direction) => clip.Directions[direction].Select(i => sprites[i]).ToArray();
                return new DirectionalClip
                {
                    id = clip.Id,
                    framesPerSecond = clip.FramesPerSecond,
                    loop = clip.Loop,
                    east = Frames(0),
                    northEast = Frames(1),
                    north = Frames(2),
                    northWest = Frames(3),
                    west = Frames(4),
                    southWest = Frames(5),
                    south = Frames(6),
                    southEast = Frames(7),
                };
            }).ToArray();
            EditorUtility.SetDirty(set);
            FlareSets[lookName] = set;
            return set;
        }

        /// <summary>The animation sets made in this build, by look (cleared when a build starts).</summary>
        private static readonly Dictionary<string, CharacterAnimationSet> FlareSets = new Dictionary<string, CharacterAnimationSet>();

        /// <summary>
        /// The Flare look of a source-game monster, by its name: ants and bugs, wyrms and drakes, the undead (warriors,
        /// casters, archers), zombies and lost souls, horned beasts and brutes, goblin folk; anything else by level.
        /// </summary>
        private static string MonsterLook(string name, int level)
        {
            string n = (name ?? "").ToLowerInvariant();
            bool Has(params string[] words) => words.Any(w => n.Contains(w));
            // Whole camps look alike: the source game's Rotsworn cultists and the Korthian raiders.
            if (Has("blightchosen")) return Has("acolyte") ? "SkeletonMage" : "GoblinElite";
            if (Has("zhendale")) return Has("archer") ? "SkeletonArcher" : "Minotaur";
            if (Has("fire antwig", "firemite")) return "FireAnt";
            if (Has("ice antwig", "frostmite")) return "IceAnt";
            if (Has("broodmother")) return "Antlion";
            if (Has("antling", "antwig")) return "AntlionSmall";
            if (Has("antipede", "pincer", "spider", "arachnid", "broodmother", "howler", "shadowclaw")) return "Antlion";
            if (Has("flame", "fire cokatrice", "fire quazit", "balagos", "tchazzar", "klauth")) return "WyvernFire";
            if (Has("wyrm", "hydra", "roc", "cokatrice", "serpent")) return "Wyvern";
            if (Has("archer")) return "SkeletonArcher";
            if (Has("mage", "sorcerer", "seer of", "watcher", "cleric", "shaman", "quazit")) return "SkeletonMage";
            if (Has("skelet", "guard", "loyalist", "knight", "centurion", "tomb", "uninterred", "maximus")) return "Skeleton";
            if (Has("damned", "lost soul", "horror", "risen", "servant", "drauglyen", "defiler")) return "Zombie";
            if (Has("horned", "charger", "beast", "brute", "yargol", "fasthorn", "tantorus", "elite", "morgall")) return "Minotaur";
            if (Has("trog", "tasloi", "bandit", "terror clan", "goblin", "overseer", "bushwhacker", "plunderer")) return level >= 6 ? "GoblinElite" : "Goblin";
            return level <= 5 ? "Goblin" : level <= 12 ? "GoblinElite" : "Minotaur";
        }

        /// <summary>How big a look is drawn (the art's own sizes differ: a small antling, a huge wyvern).</summary>
        private static float LookScale(string look)
        {
            switch (look)
            {
                case "AntlionSmall": return 1.1f;
                case "Wyvern":
                case "WyvernFire": return 0.75f;
                default: return 1f;
            }
        }

        // The monsters' stats by level tier (goblin 1-2, brute 3-5, orc 6+) and the boss; a prefab per look and tier.
        private static MonsterSpec[] tierSpecs;
        private static MonsterSpec bossSpec;
        private static Sprite monsterBarSprite;
        private static GameObject monsterLootBag;
        private static RPG.Items.AffixCatalog monsterAffixes;
        private static readonly Dictionary<string, GameObject> LookPrefabs = new Dictionary<string, GameObject>();

        /// <summary>The prefab for a source-game monster: the stats of its level tier (or the boss's) with its Flare look.</summary>
        private static GameObject MonsterPrefabFor(string name, int level, bool boss)
        {
            string look = MonsterLook(name, level);
            int tier = boss ? 3 : level <= 2 ? 0 : level <= 5 ? 1 : 2;
            string key = $"{look}_{tier}";
            if (LookPrefabs.TryGetValue(key, out GameObject cached)) return cached;

            MonsterSpec spec = boss ? bossSpec : tierSpecs[tier];
            spec.Name = $"{spec.Name.Replace(" ", "")}_{look}";
            spec.Animations = FlareAnimationSet(look);
            spec.Scale = LookScale(look) * (boss ? 1.3f : 1f);
            GameObject prefab = CreateMonsterPrefab(spec, monsterBarSprite, monsterLootBag, monsterAffixes,
                boss ? (Action<GameObject>)(monster => monster.AddComponent<BossMechanics>()) : null);
            LookPrefabs[key] = prefab;
            return prefab;
        }
    }
}
