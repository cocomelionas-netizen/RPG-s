using System.IO;
using UnityEditor;
using UnityEngine;

namespace RPG.EditorTools
{
    /// <summary>
    /// Generates simple soft-shaded placeholder sprites so the game can be tested
    /// before real hand-painted art exists. Replace these files with real art later,
    /// keeping the same size, pixels per unit and pivot.
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        public const string Folder = "Assets/_Project/Art/Placeholder";

        // 256 px = 1 world unit. An isometric tile is 256x128 px = 1 x 0.5 units.
        public const int PixelsPerUnit = 256;
        private const int TileWidth = 256;
        private const int TileHeight = 128;

        public static Sprite GroundTile(string name, Color baseColor, Color accentColor, int seed)
        {
            var texture = new Texture2D(TileWidth, TileHeight, TextureFormat.RGBA32, false);
            float halfW = TileWidth * 0.5f;
            float halfH = TileHeight * 0.5f;

            for (int y = 0; y < TileHeight; y++)
            for (int x = 0; x < TileWidth; x++)
            {
                // Diamond distance: 0 at the center, 1 on the edge. Slightly oversized to hide seams.
                float d = Mathf.Abs(x + 0.5f - halfW) / halfW + Mathf.Abs(y + 0.5f - halfH) / halfH;
                if (d > 1f + 1.5f / halfH)
                {
                    texture.SetPixel(x, y, Color.clear);
                    continue;
                }

                float broad = Mathf.PerlinNoise(seed * 13.1f + x * 0.025f, seed * 7.7f + y * 0.05f);
                float fine = Mathf.PerlinNoise(seed * 3.3f + x * 0.15f, seed * 5.9f + y * 0.3f);
                Color color = Color.Lerp(baseColor, accentColor, broad * 0.8f + fine * 0.2f);
                color *= Mathf.Lerp(1f, 0.93f, Mathf.InverseLerp(0.85f, 1f, d));
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }

            return SaveSprite(texture, name, new Vector2(0.5f, 0.5f));
        }

        public static Sprite Rock(string name, int seed)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var center = new Vector2(128f, 100f);
            var radius = new Vector2(92f, 76f);
            var light = new Vector2(-0.6f, 0.8f).normalized;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2((x - center.x) / radius.x, (y - center.y) / radius.y);
                float wobble = (Mathf.PerlinNoise(seed + x * 0.04f, seed + y * 0.04f) - 0.5f) * 0.25f;
                float r = p.magnitude + wobble;

                // Soft contact shadow under the rock.
                Vector2 s = new Vector2((x - center.x) / (radius.x * 1.1f), (y - 40f) / 22f);
                float shadow = Mathf.Clamp01(1f - s.magnitude) * 0.45f;

                if (r > 1f)
                {
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, shadow));
                    continue;
                }

                float shade = Mathf.Clamp01(0.55f + Vector2.Dot(p, light) * 0.45f);
                float grain = Mathf.PerlinNoise(seed * 2f + x * 0.2f, y * 0.2f) * 0.15f;
                Color color = Color.Lerp(new Color(0.22f, 0.21f, 0.24f), new Color(0.62f, 0.6f, 0.58f), shade + grain);
                color.a = 1f;
                texture.SetPixel(x, y, color);
            }

            // Pivot at the rock's base so it sorts and sits correctly on its tile.
            return SaveSprite(texture, name, new Vector2(0.5f, 0.2f));
        }

        public static Sprite Character(string name, Color cloakColor)
        {
            const int width = 128;
            const int height = 192;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var skin = new Color(0.93f, 0.78f, 0.64f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float cx = x + 0.5f - width * 0.5f;
                Color color = Color.clear;

                // Ground shadow.
                float shadow = Mathf.Clamp01(1f - new Vector2(cx / 44f, (y - 14f) / 12f).magnitude) * 0.4f;
                if (shadow > 0f) color = new Color(0f, 0f, 0f, shadow);

                // Cloak: a rounded cone from the feet to the shoulders.
                float bodyHalfWidth = Mathf.Lerp(34f, 22f, Mathf.InverseLerp(14f, 120f, y));
                if (y >= 14 && y <= 124 && Mathf.Abs(cx) <= bodyHalfWidth)
                {
                    float side = cx / bodyHalfWidth;
                    color = cloakColor * Mathf.Lerp(1.15f, 0.65f, (side + 1f) * 0.5f);
                    color.a = 1f;
                }

                // Head.
                float head = new Vector2(cx, y - 144f).magnitude;
                if (head <= 22f)
                {
                    color = skin * Mathf.Lerp(1.05f, 0.75f, Mathf.Clamp01((cx + 10f) / 32f));
                    color.a = 1f;
                }

                texture.SetPixel(x, y, color);
            }

            // Pivot at the feet: position = where the character stands.
            return SaveSprite(texture, name, new Vector2(0.5f, 14f / height));
        }

        private static Sprite SaveSprite(Texture2D texture, string name, Vector2 pivot)
        {
            texture.Apply();
            Directory.CreateDirectory(Folder);
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
