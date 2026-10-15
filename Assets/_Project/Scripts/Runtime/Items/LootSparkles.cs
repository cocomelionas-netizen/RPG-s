using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// Twinkling sparkles and a soft glow over something with loot in it (a monster's drop), so it can be seen and
    /// clicked. Purely visual; builds its own sprites.
    /// </summary>
    public class LootSparkles : MonoBehaviour
    {
        private const int SparkleCount = 6;
        private static Sprite sparkleSprite;
        private static Sprite glowSprite;

        private readonly SpriteRenderer[] sparkles = new SpriteRenderer[SparkleCount];
        private readonly float[] phases = new float[SparkleCount];
        private SpriteRenderer glow;

        private static readonly Color SparkleColor = new Color(1f, 0.92f, 0.55f);
        private static readonly Color GlowColor = new Color(1f, 0.85f, 0.35f, 0.35f);

        /// <summary>Adds sparkles to <paramref name="parent"/>, drawn over <paramref name="over"/> (null: default order).</summary>
        public static LootSparkles AddTo(Transform parent, SpriteRenderer over)
        {
            var root = new GameObject("Loot Sparkles");
            root.transform.SetParent(parent, false);
            var sparklesComponent = root.AddComponent<LootSparkles>();
            sparklesComponent.Build(over);
            return sparklesComponent;
        }

        private void Build(SpriteRenderer over)
        {
            if (sparkleSprite == null) sparkleSprite = MakeSprite(16, true);
            if (glowSprite == null) glowSprite = MakeSprite(32, false);
            int layer = over != null ? over.sortingLayerID : 0;
            int order = over != null ? over.sortingOrder : 0;

            glow = new GameObject("Glow").AddComponent<SpriteRenderer>();
            glow.transform.SetParent(transform, false);
            glow.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            glow.transform.localScale = new Vector3(1.6f, 0.9f, 1f);
            glow.sprite = glowSprite;
            glow.color = GlowColor;
            glow.sortingLayerID = layer;
            glow.sortingOrder = order - 1;

            for (int i = 0; i < SparkleCount; i++)
            {
                var sparkle = new GameObject("Sparkle").AddComponent<SpriteRenderer>();
                sparkle.transform.SetParent(transform, false);
                sparkle.sprite = sparkleSprite;
                sparkle.color = SparkleColor;
                sparkle.sortingLayerID = layer;
                sparkle.sortingOrder = order + 1;
                sparkles[i] = sparkle;
                phases[i] = i / (float)SparkleCount;
            }
        }

        private void Update()
        {
            float time = Time.time;
            for (int i = 0; i < SparkleCount; i++)
            {
                // Each sparkle rises from around the loot, twinkles and fades, then starts again somewhere else.
                float cycle = (time * 0.6f + phases[i]) % 1f;
                float angle = (i * 2.4f + Mathf.Floor(time * 0.6f + phases[i]) * 1.7f) % (Mathf.PI * 2f);
                Vector3 start = new Vector3(Mathf.Cos(angle) * 0.22f, Mathf.Sin(angle) * 0.1f + 0.08f, 0f);
                sparkles[i].transform.localPosition = start + new Vector3(0f, cycle * 0.35f, 0f);
                float twinkle = Mathf.Sin(cycle * Mathf.PI);
                float size = (0.12f + 0.1f * Mathf.Abs(Mathf.Sin(time * 9f + i))) * twinkle;
                sparkles[i].transform.localScale = new Vector3(size, size, 1f) * 4f;
                Color color = SparkleColor;
                color.a = twinkle;
                sparkles[i].color = color;
            }
            Color glowColor = GlowColor;
            glowColor.a = GlowColor.a * (0.7f + 0.3f * Mathf.Sin(time * 3f));
            glow.color = glowColor;
        }

        /// <summary>A four-pointed star (sparkle) or a soft round glow, white so it can be tinted.</summary>
        private static Sprite MakeSprite(int size, bool star)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            float half = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Abs(x - half) / half;
                float dy = Mathf.Abs(y - half) / half;
                float alpha = star
                    ? Mathf.Clamp01(1f - Mathf.Min(dx * 4f + dy, dy * 4f + dx)) + Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2.5f)
                    : Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha * alpha)));
            }
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size * 4f);
        }
    }
}
