using System.Collections.Generic;
using RPG.Combat;
using RPG.Dungeons;
using RPG.Monsters;
using RPG.Navigation;
using RPG.Vendors;
using RPG.World;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The zone name and a square map around the local player: walkable ground, obstacles, and dots for
    /// players, monsters, vendors, portals and waystones. Each map's picture is drawn once from its navigation grid.
    /// Builds its own UI; place it on a RectTransform about 240 x 270 in the top right of the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class Minimap : MonoBehaviour
    {
        [SerializeField, Min(50f)] private float mapSize = 220f;
        [Tooltip("World units shown from edge to edge.")]
        [SerializeField, Min(5f)] private float worldSpan = 30f;
        [Tooltip("Texture pixels per world unit when drawing a map.")]
        [SerializeField, Min(1)] private int texturePixelsPerUnit = 4;

        private static readonly Color32 Walkable = new Color32(70, 92, 48, 255);
        private static readonly Color32 Blocked = new Color32(28, 40, 22, 255);
        private static readonly Color32 Outside = new Color32(8, 8, 8, 255);
        private static readonly Color SelfColor = Color.white;
        private static readonly Color PlayerColor = new Color(0.4f, 0.8f, 1f);
        private static readonly Color MonsterColor = new Color(0.95f, 0.2f, 0.15f);
        private static readonly Color VendorColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color PortalColor = new Color(0.75f, 0.45f, 1f);
        private static readonly Color WaystoneColor = new Color(0.3f, 0.95f, 0.9f);

        private readonly Dictionary<WorldNavigation, Texture2D> pictures = new Dictionary<WorldNavigation, Texture2D>();
        private readonly List<Image> dots = new List<Image>();
        private GameObject panel;
        private Text zoneText;
        private RectTransform view;
        private RawImage picture;
        private int usedDots;

        private void Awake()
        {
            var root = (RectTransform)transform;
            panel = UIFactory.CreateRect("Panel", root).gameObject;
            UIFactory.Stretch((RectTransform)panel.transform);

            zoneText = UIFactory.CreateText("Zone", panel.transform, 20, TextAnchor.LowerCenter);
            UIFactory.Place(zoneText.rectTransform, new Vector2(0.5f, 1f), Vector2.zero, new Vector2(mapSize + 20f, 28f));
            zoneText.color = new Color(1f, 0.88f, 0.6f);
            zoneText.fontStyle = FontStyle.Bold;

            Image frame = UIFactory.CreateImage("Frame", panel.transform, new Color(0.45f, 0.34f, 0.18f, 0.95f));
            frame.raycastTarget = false;
            UIFactory.Place(frame.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -32f), Vector2.one * (mapSize + 8f));

            Image background = UIFactory.CreateImage("View", frame.transform, Outside);
            background.raycastTarget = false;
            UIFactory.Place(background.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * mapSize);
            background.gameObject.AddComponent<RectMask2D>();
            view = background.rectTransform;

            picture = new GameObject("Map", typeof(RectTransform)).AddComponent<RawImage>();
            picture.transform.SetParent(view, false);
            picture.raycastTarget = false;
            picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        }

        private void OnDestroy()
        {
            foreach (Texture2D texture in pictures.Values)
                if (texture != null) Destroy(texture);
        }

        private void LateUpdate()
        {
            Health self = UIFactory.LocalPlayer<Health>();
            bool show = self != null && self.IsSpawned;
            if (panel.activeSelf != show) panel.SetActive(show);
            if (!show) return;

            Vector2 center = self.transform.position;
            float scale = mapSize / worldSpan;
            WorldNavigation map = WorldNavigation.At(center);
            zoneText.text = Zone.NameAt(center);

            picture.enabled = map != null;
            if (map != null)
            {
                Rect bounds = map.WorldBounds;
                picture.texture = Picture(map);
                picture.rectTransform.sizeDelta = bounds.size * scale;
                picture.rectTransform.anchoredPosition = (bounds.center - center) * scale;
            }

            usedDots = 0;
            foreach (Portal portal in Portal.Portals)
                if (portal != null) Dot(portal.transform.position, center, scale, PortalColor, 9f);
            foreach (Vendor vendor in Vendor.All)
                if (vendor != null) Dot(vendor.transform.position, center, scale, VendorColor, 8f);
            foreach (Waystone stone in Waystone.All)
                if (stone != null) Dot(stone.transform.position, center, scale, WaystoneColor, 9f);
            foreach (Health unit in Health.Spawned)
            {
                if (unit == null || unit == self || unit.IsDead) continue;
                if (unit.Faction == Faction.Player) Dot(unit.transform.position, center, scale, PlayerColor, 8f);
                else if (unit.GetComponent<MonsterAI>() != null) Dot(unit.transform.position, center, scale, MonsterColor, 6f);
            }
            Dot(center, center, scale, SelfColor, 10f);

            for (int i = usedDots; i < dots.Count; i++)
                if (dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(false);
        }

        private void Dot(Vector2 position, Vector2 center, float scale, Color color, float size)
        {
            Vector2 offset = (position - center) * scale;
            float half = mapSize * 0.5f;
            if (Mathf.Abs(offset.x) > half || Mathf.Abs(offset.y) > half) return;

            if (usedDots == dots.Count)
            {
                Image created = UIFactory.CreateImage("Dot", view, color);
                created.sprite = UIFactory.CircleSprite;
                created.raycastTarget = false;
                created.rectTransform.anchorMin = created.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                created.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);
                dots.Add(created);
            }

            Image dot = dots[usedDots++];
            if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
            dot.transform.SetAsLastSibling();
            dot.color = color;
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.rectTransform.anchoredPosition = offset;
        }

        /// <summary>The map's walkable ground drawn into a texture (made once per map).</summary>
        private Texture2D Picture(WorldNavigation map)
        {
            if (pictures.TryGetValue(map, out Texture2D texture) && texture != null) return texture;
            ForgetRemovedMaps();

            Rect bounds = map.WorldBounds;
            int width = Mathf.Clamp(Mathf.CeilToInt(bounds.width * texturePixelsPerUnit), 1, 1024);
            int height = Mathf.Clamp(Mathf.CeilToInt(bounds.height * texturePixelsPerUnit), 1, 1024);
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
            pictures[map] = texture;
            return texture;
        }

        /// <summary>Dungeon maps are removed when their instance closes; drop their pictures too.</summary>
        private void ForgetRemovedMaps()
        {
            var removed = new List<WorldNavigation>();
            foreach (KeyValuePair<WorldNavigation, Texture2D> entry in pictures)
                if (entry.Key == null) removed.Add(entry.Key);
            foreach (WorldNavigation map in removed)
            {
                if (pictures[map] != null) Destroy(pictures[map]);
                pictures.Remove(map);
            }
        }
    }
}
