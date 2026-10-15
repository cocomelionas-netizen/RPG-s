using System.Collections.Generic;
using RPG.Navigation;
using UnityEngine;

namespace RPG.UI
{
    /// <summary>
    /// A map's ground drawn into a texture from its navigation grid (walkable, blocked, outside), made once per map
    /// and shared by the minimap and the world map (M).
    /// </summary>
    public static class MapPicture
    {
        private static readonly Color32 Walkable = new Color32(70, 92, 48, 255);
        private static readonly Color32 Blocked = new Color32(28, 40, 22, 255);
        private static readonly Color32 Outside = new Color32(8, 8, 8, 255);

        /// <summary>Texture pixels per world unit (lower for very big maps, see <see cref="MaxSize"/>).</summary>
        private const int PixelsPerUnit = 4;
        private const int MaxSize = 2048;

        private static readonly Dictionary<WorldNavigation, Texture2D> Pictures = new Dictionary<WorldNavigation, Texture2D>();

        public static Color OutsideColor => Outside;

        /// <summary>The picture of a map, covering <see cref="WorldNavigation.WorldBounds"/>.</summary>
        public static Texture2D Of(WorldNavigation map)
        {
            if (map == null) return null;
            if (Pictures.TryGetValue(map, out Texture2D texture) && texture != null) return texture;
            ForgetRemovedMaps();

            Rect bounds = map.WorldBounds;
            float scale = Mathf.Min(PixelsPerUnit, MaxSize / Mathf.Max(1f, Mathf.Max(bounds.width, bounds.height)));
            int width = Mathf.Clamp(Mathf.CeilToInt(bounds.width * scale), 1, MaxSize);
            int height = Mathf.Clamp(Mathf.CeilToInt(bounds.height * scale), 1, MaxSize);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                var point = new Vector2(bounds.xMin + (x + 0.5f) / width * bounds.width, bounds.yMin + (y + 0.5f) / height * bounds.height);
                pixels[y * width + x] = !map.Contains(point) ? Outside : map.IsWalkable(point) ? Walkable : Blocked;
            }

            texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply();
            Pictures[map] = texture;
            return texture;
        }

        /// <summary>Dungeon maps are removed when their instance closes; drop their pictures too.</summary>
        private static void ForgetRemovedMaps()
        {
            var removed = new List<WorldNavigation>();
            foreach (KeyValuePair<WorldNavigation, Texture2D> entry in Pictures)
                if (entry.Key == null) removed.Add(entry.Key);
            foreach (WorldNavigation map in removed)
            {
                if (Pictures[map] != null) Object.Destroy(Pictures[map]);
                Pictures.Remove(map);
            }
        }
    }
}
