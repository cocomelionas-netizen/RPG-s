using System;
using System.Collections.Generic;
using System.Linq;
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
        }

        private static Texture2D FlareAtlas(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
            {
                Debug.LogError($"[TestScene] Missing art {path} (run Tools/Art/flare.py).");
                return null;
            }
            importer.textureType = TextureImporterType.Default;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 4096;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
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
                Water = Kind("Water", Tile.ColliderType.None),
                // Rock0-3 are boulders, Rock4-7 tall pillars.
                Walls = Kind("Rock", Tile.ColliderType.Grid, i => i >= 4),
                Boulders = Kind("Rock", Tile.ColliderType.Grid, i => i < 4),
                Trees = Kind("Tree", Tile.ColliderType.Grid),
                Plants = Kind("Plant", Tile.ColliderType.Grid),
                // Plant6-13: reeds and tall grass, drawn on open ground without blocking.
                Decor = Kind("Plant", Tile.ColliderType.None, i => i >= 6),
            };
        }

        /// <summary>An animation set from a Flare look (<see cref="FlareArtData.Looks"/>): Idle, Walk and Attack in eight directions.</summary>
        private static CharacterAnimationSet FlareAnimationSet(string lookName)
        {
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
            return set;
        }

        /// <summary>
        /// The Flare look of a source-game monster, by its name: ants and bugs, wyrms and drakes, the undead (warriors,
        /// casters, archers), zombies and lost souls, horned beasts and brutes, goblin folk; anything else by level.
        /// </summary>
        private static string MonsterLook(string name, int level)
        {
            string n = (name ?? "").ToLowerInvariant();
            bool Has(params string[] words) => words.Any(w => n.Contains(w));
            if (Has("fire antwig", "firemite")) return "FireAnt";
            if (Has("ice antwig", "frostmite")) return "IceAnt";
            if (Has("antling", "antwig")) return "AntlionSmall";
            if (Has("antipede", "pincer", "spider", "arachnid", "broodmother", "howler", "shadowclaw")) return "Antlion";
            if (Has("flame", "fire cokatrice", "fire quazit", "balagos", "tchazzar", "klauth")) return "WyvernFire";
            if (Has("wyrm", "hydra", "roc", "cokatrice", "serpent")) return "Wyvern";
            if (Has("archer")) return "SkeletonArcher";
            if (Has("mage", "sorcerer", "seer", "watcher", "cleric", "shaman", "acolyte", "quazit")) return "SkeletonMage";
            if (Has("skelet", "guard", "loyalist", "knight", "centurion", "tomb", "uninterred", "maximus")) return "Skeleton";
            if (Has("damned", "lost soul", "horror", "risen", "servant", "drauglyen", "defiler")) return "Zombie";
            if (Has("horned", "charger", "beast", "brute", "yargol", "fasthorn", "tantorus", "elite", "morgall")) return "Minotaur";
            if (Has("trog", "tasloi", "bandit", "blightchosen", "terror clan", "goblin")) return level >= 6 ? "GoblinElite" : "Goblin";
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
