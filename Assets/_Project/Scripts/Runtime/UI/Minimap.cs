using System.Collections.Generic;
using RPG.Combat;
using RPG.Dungeons;
using RPG.Navigation;
using RPG.Vendors;
using RPG.World;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The zone name and a square map around the local player: walkable ground, obstacles, and dots for
    /// players, monsters, vendors, portals, waystones, chests and the
    /// marked place (<see cref="MapPin"/>). The picture comes from <see cref="MapPicture"/>.
    /// Builds its own UI; place it on a RectTransform about 240 x 270 in the top right of the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class Minimap : MonoBehaviour
    {
        [SerializeField, Min(50f)] private float mapSize = 220f;
        [Tooltip("World units shown from edge to edge.")]
        [SerializeField, Min(5f)] private float worldSpan = 30f;
        private static readonly Color SelfColor = Color.white;
        private static readonly Color PlayerColor = new Color(0.4f, 0.8f, 1f);
        private static readonly Color MonsterColor = new Color(0.95f, 0.2f, 0.15f);
        private static readonly Color VendorColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color PortalColor = new Color(0.75f, 0.45f, 1f);
        private static readonly Color WaystoneColor = new Color(0.3f, 0.95f, 0.9f);
        private static readonly Color ChestColor = new Color(0.85f, 0.6f, 0.3f);

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

            Image background = UIFactory.CreateImage("View", frame.transform, MapPicture.OutsideColor);
            background.raycastTarget = false;
            UIFactory.Place(background.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one * mapSize);
            background.gameObject.AddComponent<RectMask2D>();
            // The map and its dots move every frame: a canvas of their own keeps that from rebuilding the whole HUD.
            background.gameObject.AddComponent<Canvas>();
            view = background.rectTransform;

            picture = new GameObject("Map", typeof(RectTransform)).AddComponent<RawImage>();
            picture.transform.SetParent(view, false);
            picture.raycastTarget = false;
            picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
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
                picture.texture = MapPicture.Of(map);
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
            foreach (LootChest chest in LootChest.All)
                if (chest != null) Dot(chest.transform.position, center, scale, ChestColor, 6f);
            foreach (Health unit in Health.Spawned)
            {
                if (unit == null || unit == self || unit.IsDead) continue;
                if (unit.Faction == Faction.Player) Dot(unit.transform.position, center, scale, PlayerColor, 8f);
                else if (unit.Faction == Faction.Monster) Dot(unit.transform.position, center, scale, MonsterColor, 6f);
            }
            // A marked place (a guard's directions): at the edge, pointing the way, while it is off the map.
            MapPin.CheckReached(center);
            if (MapPin.Active) Dot(MapPin.Position, center, scale, MapPin.PinColor, 11f, true);
            Dot(center, center, scale, SelfColor, 10f);

            for (int i = usedDots; i < dots.Count; i++)
                if (dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(false);
        }

        private void Dot(Vector2 position, Vector2 center, float scale, Color color, float size, bool keepAtEdge = false)
        {
            Vector2 offset = (position - center) * scale;
            float half = mapSize * 0.5f;
            if (Mathf.Abs(offset.x) > half || Mathf.Abs(offset.y) > half)
            {
                if (!keepAtEdge) return;
                float inside = half - size * 0.5f;
                offset *= inside / Mathf.Max(Mathf.Abs(offset.x), Mathf.Abs(offset.y));
            }

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
            // Dots are used in creation order after the map picture, so the last one drawn (you) is on top without reordering.
            if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
            dot.color = color;
            dot.rectTransform.sizeDelta = new Vector2(size, size);
            dot.rectTransform.anchoredPosition = offset;
        }
    }
}
