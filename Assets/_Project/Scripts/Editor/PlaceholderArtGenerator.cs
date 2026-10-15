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

        /// <summary>
        /// One animation frame of the placeholder character facing a screen direction.
        /// Eyes and a chest buckle show which way it faces; feet and a body bob show walking.
        /// </summary>
        /// <param name="facing">Normalized screen-space facing direction.</param>
        /// <param name="bob">Vertical body offset in pixels.</param>
        /// <param name="stride">Foot offset along the facing direction, -1..1.</param>
        /// <param name="swingDegrees">When set, draws a sword swung this many degrees from the facing direction (attack frames).</param>
        /// <param name="skinColor">Face color; defaults to a human skin tone.</param>
        public static Sprite CharacterFrame(string folder, string name, Color cloakColor, Vector2 facing, float bob, float stride,
            float? swingDegrees = null, Color? skinColor = null)
        {
            const int width = 128;
            const int height = 192;
            const float feetY = 14f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            Color skin = skinColor ?? new Color(0.93f, 0.78f, 0.64f);
            var hood = cloakColor * 0.7f;
            var dark = new Color(0.12f, 0.1f, 0.1f);
            var gold = new Color(0.95f, 0.78f, 0.3f);

            bool facesCamera = facing.y < 0.35f;
            var leftFoot = new Vector2(-11f, feetY + 3f) + new Vector2(facing.x * 9f, facing.y * 4f) * stride;
            var rightFoot = new Vector2(11f, feetY + 3f) - new Vector2(facing.x * 9f, facing.y * 4f) * stride;
            var headCenter = new Vector2(0f, 144f + bob);
            var eyeCenter = headCenter + new Vector2(facing.x * 11f, -3f);
            float eyeSpread = Mathf.Lerp(7f, 2.5f, Mathf.Abs(facing.x));
            var buckle = new Vector2(facing.x * 13f, 70f + bob);
            var steel = new Color(0.85f, 0.87f, 0.9f);
            var hand = new Vector2(facing.x * 18f, 92f + bob + facing.y * 6f);
            Vector2 swordTip = hand;
            if (swingDegrees.HasValue)
            {
                float angle = (Mathf.Atan2(facing.y, facing.x) * Mathf.Rad2Deg + swingDegrees.Value) * Mathf.Deg2Rad;
                // Screen-space sword, foreshortened vertically like the isometric ground.
                swordTip = hand + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * 0.6f + 0.35f) * 52f;
            }

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - width * 0.5f, y + 0.5f);
                Color color = Color.clear;

                // Ground shadow.
                float shadow = Mathf.Clamp01(1f - new Vector2(p.x / 44f, (p.y - feetY) / 12f).magnitude) * 0.4f;
                if (shadow > 0f) color = new Color(0f, 0f, 0f, shadow);

                // Feet.
                if (Ellipse(p, leftFoot, 9f, 5f) || Ellipse(p, rightFoot, 9f, 5f))
                    color = dark;

                // Cloak: a rounded cone from the feet to the shoulders.
                float bodyBottom = feetY + 6f + bob;
                float bodyTop = 124f + bob;
                float bodyHalfWidth = Mathf.Lerp(34f, 22f, Mathf.InverseLerp(bodyBottom, bodyTop, p.y));
                if (p.y >= bodyBottom && p.y <= bodyTop && Mathf.Abs(p.x) <= bodyHalfWidth)
                {
                    float side = p.x / bodyHalfWidth;
                    color = cloakColor * Mathf.Lerp(1.15f, 0.65f, (side + 1f) * 0.5f);
                    color.a = 1f;
                }

                if (facesCamera && (p - buckle).magnitude <= 5f)
                    color = gold;

                // Head: face when looking towards the camera, hood when looking away.
                if ((p - headCenter).magnitude <= 22f)
                {
                    float light = Mathf.Lerp(1.05f, 0.75f, Mathf.Clamp01((p.x + 10f) / 32f));
                    color = (facesCamera ? skin : hood) * light;
                    color.a = 1f;

                    if (facesCamera &&
                        ((p - (eyeCenter + Vector2.left * eyeSpread)).magnitude <= 2.6f ||
                         (p - (eyeCenter + Vector2.right * eyeSpread)).magnitude <= 2.6f))
                        color = dark;
                }

                if (swingDegrees.HasValue && DistanceToSegment(p, hand, swordTip) <= 2.5f)
                    color = steel;

                texture.SetPixel(x, y, color);
            }

            // Pivot at the feet: position = where the character stands.
            return SaveSprite(texture, folder, name, new Vector2(0.5f, feetY / height));
        }

        public static Sprite TrainingDummy(string name)
        {
            const int width = 160;
            const int height = 288;
            const float baseY = 20f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var wood = new Color(0.45f, 0.3f, 0.17f);
            var straw = new Color(0.82f, 0.7f, 0.38f);
            var rope = new Color(0.55f, 0.42f, 0.25f);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - width * 0.5f, y + 0.5f);
                Color color = Color.clear;

                float shadow = Mathf.Clamp01(1f - new Vector2(p.x / 40f, (p.y - baseY) / 12f).magnitude) * 0.4f;
                if (shadow > 0f) color = new Color(0f, 0f, 0f, shadow);

                // Post and cross beam.
                if (p.y >= baseY && p.y <= 250f && Mathf.Abs(p.x) <= 7f) color = wood * Mathf.Lerp(1.15f, 0.75f, (p.x + 7f) / 14f);
                if (p.y >= 170f && p.y <= 182f && Mathf.Abs(p.x) <= 62f) color = wood * 0.95f;

                // Straw body and head with a little noise.
                float grain = Mathf.PerlinNoise(x * 0.3f, y * 0.08f) * 0.3f;
                if (Ellipse(p, new Vector2(0f, 140f), 34f, 52f)) { color = straw * (0.85f + grain); color.a = 1f; }
                if (Ellipse(p, new Vector2(0f, 220f), 24f, 26f)) { color = straw * (0.9f + grain); color.a = 1f; }
                if (Mathf.Abs(p.y - 120f) < 3f && Mathf.Abs(p.x) < 33f) color = rope;
                if (Mathf.Abs(p.y - 196f) < 3f && Mathf.Abs(p.x) < 14f) color = rope;

                texture.SetPixel(x, y, color);
            }

            return SaveSprite(texture, Folder, name, new Vector2(0.5f, baseY / height));
        }

        /// <summary>A small white sprite, 1 world unit wide, pivot on the left edge: for bars that fill from the left.</summary>
        public static Sprite BarPixel(string name)
        {
            const int size = 4;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                texture.SetPixel(x, y, Color.white);
            return SaveSprite(texture, Folder, name, new Vector2(0f, 0.5f), size, FilterMode.Point);
        }

        public static Sprite Tree(string name, int seed)
        {
            const int width = 256;
            const int height = 448;
            const float baseY = 36f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var light = new Vector2(-0.6f, 0.8f).normalized;
            var leafDark = new Color(0.12f, 0.26f, 0.12f);
            var leafLight = new Color(0.36f, 0.56f, 0.24f);
            Vector2[] blobs = { new Vector2(0f, 300f), new Vector2(-52f, 250f), new Vector2(54f, 246f), new Vector2(0f, 370f) };
            float[] blobRadius = { 92f, 64f, 62f, 58f };

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - width * 0.5f, y + 0.5f);
                Color color = Color.clear;

                float shadow = Mathf.Clamp01(1f - new Vector2(p.x / 90f, (p.y - baseY) / 24f).magnitude) * 0.45f;
                if (shadow > 0f) color = new Color(0f, 0f, 0f, shadow);

                // Trunk, slightly wider at the base.
                float trunkHalfWidth = Mathf.Lerp(20f, 12f, Mathf.InverseLerp(baseY, 240f, p.y));
                if (p.y >= baseY && p.y <= 260f && Mathf.Abs(p.x) <= trunkHalfWidth)
                {
                    color = new Color(0.36f, 0.24f, 0.15f) * Mathf.Lerp(1.15f, 0.7f, (p.x / trunkHalfWidth + 1f) * 0.5f);
                    color.a = 1f;
                }

                // Canopy: overlapping noisy circles shaded from the top-left.
                for (int i = 0; i < blobs.Length; i++)
                {
                    Vector2 local = (p - blobs[i]) / blobRadius[i];
                    float wobble = (Mathf.PerlinNoise(seed + x * 0.05f, seed + y * 0.05f) - 0.5f) * 0.3f;
                    if (local.magnitude + wobble > 1f) continue;

                    float shade = Mathf.Clamp01(0.5f + Vector2.Dot(local, light) * 0.5f);
                    float grain = Mathf.PerlinNoise(seed * 3f + x * 0.18f, y * 0.18f) * 0.25f;
                    color = Color.Lerp(leafDark, leafLight, shade * 0.8f + grain);
                    color.a = 1f;
                }

                texture.SetPixel(x, y, color);
            }

            // Pivot at the trunk base so characters sort in front of / behind the tree correctly.
            return SaveSprite(texture, Folder, name, new Vector2(0.5f, baseY / height));
        }

        /// <summary>A standing oval of swirling violet light, pivot at its base.</summary>
        public static Sprite Portal(string name)
        {
            const int width = 160;
            const int height = 256;
            const float baseY = 16f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var center = new Vector2(0f, 120f);
            var outer = new Color(0.45f, 0.2f, 0.8f);
            var inner = new Color(0.85f, 0.7f, 1f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - width * 0.5f, y + 0.5f);
                Color color = Color.clear;
                float shadow = Mathf.Clamp01(1f - new Vector2(p.x / 60f, (p.y - baseY) / 12f).magnitude) * 0.45f;
                if (shadow > 0f) color = new Color(0.2f, 0f, 0.3f, shadow);

                Vector2 local = new Vector2((p.x - center.x) / 58f, (p.y - center.y) / 100f);
                float r = local.magnitude;
                if (r <= 1f)
                {
                    float angle = Mathf.Atan2(local.y, local.x);
                    float swirl = Mathf.Sin(angle * 3f + r * 9f) * 0.5f + 0.5f;
                    Color glow = Color.Lerp(outer, inner, Mathf.Clamp01((1f - r) * 0.8f + swirl * 0.3f));
                    glow.a = r > 0.85f ? Mathf.Lerp(1f, 0.6f, (r - 0.85f) / 0.15f) : 0.92f;
                    color = glow;
                }
                texture.SetPixel(x, y, color);
            }
            return SaveSprite(texture, Folder, name, new Vector2(0.5f, baseY / height));
        }

        /// <summary>A grey standing stone with glowing blue-green runes, used as a teleport waystone.</summary>
        public static Sprite Waystone(string name)
        {
            const int width = 128;
            const int height = 224;
            const float baseY = 14f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var stone = new Color(0.46f, 0.47f, 0.5f);
            var dark = new Color(0.28f, 0.29f, 0.32f);
            var rune = new Color(0.35f, 1f, 0.9f);
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - width * 0.5f, y + 0.5f);
                Color color = Color.clear;
                float shadow = Mathf.Clamp01(1f - new Vector2(p.x / 50f, (p.y - baseY) / 10f).magnitude) * 0.5f;
                if (shadow > 0f) color = new Color(0f, 0f, 0f, shadow);

                // A slab that narrows towards a pointed top.
                float t = Mathf.InverseLerp(baseY, 200f, p.y);
                float halfWidth = Mathf.Lerp(30f, 18f, t) - (p.y > 182f ? (p.y - 182f) * 1.0f : 0f);
                if (p.y >= baseY && p.y <= 200f && Mathf.Abs(p.x) <= halfWidth)
                {
                    float shade = 0.75f + 0.25f * (p.x / Mathf.Max(1f, halfWidth));
                    color = Color.Lerp(dark, stone, shade);
                    float crack = Mathf.PerlinNoise(x * 0.15f, y * 0.15f);
                    if (crack > 0.72f) color *= 0.85f;
                    color.a = 1f;

                    // Three glowing runes down the middle.
                    for (int i = 0; i < 3; i++)
                    {
                        var runeCenter = new Vector2(0f, 70f + i * 38f);
                        Vector2 d = p - runeCenter;
                        bool stroke = (Mathf.Abs(d.x) < 2.2f && Mathf.Abs(d.y) < 11f)
                                      || (Mathf.Abs(d.y - d.x * 0.8f) < 2.2f && Mathf.Abs(d.x) < 8f)
                                      || (i == 1 && Mathf.Abs(d.y + d.x * 0.8f) < 2.2f && Mathf.Abs(d.x) < 8f);
                        if (stroke) color = rune;
                        else
                        {
                            float glow = Mathf.Clamp01(1f - d.magnitude / 16f) * 0.35f;
                            color = Color.Lerp(color, rune, glow);
                        }
                    }
                }
                texture.SetPixel(x, y, color);
            }
            return SaveSprite(texture, Folder, name, new Vector2(0.5f, baseY / height));
        }

        /// <summary>
        /// A box on the ground seen from the front and a little above: a crate, chest or strongbox.
        /// <paramref name="roundLid"/> gives it a domed chest lid.
        /// </summary>
        public static Sprite Container(string name, Color body, Color band, Color metal, float boxWidth, float boxHeight, bool roundLid)
        {
            const int width = 112;
            const int height = 104;
            const float baseY = 10f;
            const float topDepth = 14f;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            float halfWidth = boxWidth * 0.5f;
            float frontTop = baseY + boxHeight;
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var p = new Vector2(x + 0.5f - width * 0.5f, y + 0.5f);
                Color color = Color.clear;
                float shadow = Mathf.Clamp01(1f - new Vector2(p.x / (halfWidth + 10f), (p.y - baseY) / 8f).magnitude) * 0.45f;
                if (shadow > 0f) color = new Color(0f, 0f, 0f, shadow);

                bool inX = Mathf.Abs(p.x) <= halfWidth;
                float lidTop = roundLid ? frontTop + topDepth * Mathf.Sqrt(Mathf.Max(0f, 1f - (p.x * p.x) / (halfWidth * halfWidth))) : frontTop + topDepth;
                if (inX && p.y >= baseY && p.y <= frontTop)
                {
                    // Front face with planks, bands near the edges and a clasp in the middle.
                    float plank = Mathf.Repeat(p.y - baseY, 9f) < 1.2f ? 0.8f : 1f;
                    color = body * plank * (0.85f + 0.15f * (p.x / halfWidth + 1f) * 0.5f);
                    if (Mathf.Abs(Mathf.Abs(p.x) - (halfWidth - 6f)) < 3f) color = band;
                    if (Mathf.Abs(p.x) < 6f && Mathf.Abs(p.y - (frontTop - 8f)) < 6f) color = metal;
                    color.a = 1f;
                }
                else if (inX && p.y > frontTop && p.y <= lidTop)
                {
                    // Lid / top, lighter as it faces the light.
                    color = Color.Lerp(body, Color.white, 0.18f);
                    if (Mathf.Abs(Mathf.Abs(p.x) - (halfWidth - 6f)) < 3f) color = Color.Lerp(band, Color.white, 0.15f);
                    color.a = 1f;
                }
                if (color.a > 0.99f && (Mathf.Abs(Mathf.Abs(p.x) - halfWidth) < 1.5f || Mathf.Abs(p.y - baseY) < 1.5f || Mathf.Abs(p.y - frontTop) < 1.2f))
                    color = Color.Lerp(color, Color.black, 0.45f);
                texture.SetPixel(x, y, color);
            }
            return SaveSprite(texture, Folder, name, new Vector2(0.5f, baseY / height));
        }

        /// <summary>A small cloth sack with a golden glint, left on the ground as loot.</summary>
        public static Sprite LootSack(string name)
        {
            const int size = 96;
            const float baseY = 12f;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var cloth = new Color(0.55f, 0.38f, 0.2f);
            var gold = new Color(1f, 0.85f, 0.3f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                var p = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f);
                Color color = Color.clear;
                float shadow = Mathf.Clamp01(1f - new Vector2(p.x / 30f, (p.y - baseY) / 9f).magnitude) * 0.4f;
                if (shadow > 0f) color = new Color(0f, 0f, 0f, shadow);

                if (Ellipse(p, new Vector2(0f, 34f), 26f, 24f))
                {
                    float shade = Mathf.Lerp(1.15f, 0.7f, Mathf.InverseLerp(-26f, 26f, p.x + (p.y - 34f) * 0.3f));
                    color = cloth * shade;
                    color.a = 1f;
                }
                if (Ellipse(p, new Vector2(0f, 62f), 9f, 7f)) { color = cloth * 0.85f; color.a = 1f; }
                if (Mathf.Abs(p.y - 56f) < 2.5f && Mathf.Abs(p.x) < 10f) color = new Color(0.3f, 0.2f, 0.1f);
                if (Ellipse(p, new Vector2(-8f, 44f), 4f, 4f) || Ellipse(p, new Vector2(10f, 30f), 3f, 3f)) color = gold;
                texture.SetPixel(x, y, color);
            }
            return SaveSprite(texture, Folder, name, new Vector2(0.5f, baseY / size));
        }

        /// <summary>A soft white glowing ball for projectiles (tinted at runtime).</summary>
        public static Sprite Orb(string name)
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f).magnitude / (size * 0.5f);
                float glow = Mathf.Clamp01(1f - d);
                float alpha = Mathf.Clamp01(glow * glow * 1.6f + (d < 0.35f ? 0.6f : 0f));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
            return SaveSprite(texture, Folder, name, new Vector2(0.5f, 0.5f));
        }

        /// <summary>A flat isometric ring for ground markers (click destination, target circles).</summary>
        public static Sprite GroundRing(string name, Color color)
        {
            const int width = 128;
            const int height = 64;
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);

            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float d = new Vector2((x + 0.5f - width * 0.5f) / (width * 0.5f), (y + 0.5f - height * 0.5f) / (height * 0.5f)).magnitude;
                // Soft ring between 70% and 95% of the radius.
                float alpha = Mathf.Clamp01(1f - Mathf.Abs(d - 0.82f) / 0.13f);
                texture.SetPixel(x, y, new Color(color.r, color.g, color.b, alpha * color.a));
            }

            return SaveSprite(texture, Folder, name, new Vector2(0.5f, 0.5f));
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 0f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static bool Ellipse(Vector2 p, Vector2 center, float radiusX, float radiusY)
        {
            return new Vector2((p.x - center.x) / radiusX, (p.y - center.y) / radiusY).sqrMagnitude <= 1f;
        }

        private static Sprite SaveSprite(Texture2D texture, string name, Vector2 pivot)
        {
            return SaveSprite(texture, Folder, name, pivot);
        }

        private static Sprite SaveSprite(Texture2D texture, string folder, string name, Vector2 pivot,
            int pixelsPerUnit = PixelsPerUnit, FilterMode filterMode = FilterMode.Bilinear)
        {
            texture.Apply();
            Directory.CreateDirectory(folder);
            string path = $"{folder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = pixelsPerUnit;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.filterMode = filterMode;
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
