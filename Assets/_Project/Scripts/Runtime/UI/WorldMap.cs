using System.Collections.Generic;
using RPG.Combat;
using RPG.Controls;
using RPG.Dungeons;
using RPG.Navigation;
using RPG.Vendors;
using RPG.World;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The whole map of the place the local player is in, opened with M like Source's: the ground, named places,
    /// waystones (with their names, greyed until discovered), towns' vendors, dungeon entrances, other players and
    /// you. Closes with M, Esc or the X. Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WorldMap : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(1300f, 780f);

        private static readonly Color PanelColor = new Color(0.06f, 0.05f, 0.04f, 0.96f);
        private static readonly Color BorderColor = new Color(0.45f, 0.34f, 0.18f, 0.95f);
        private static readonly Color TitleColor = new Color(1f, 0.88f, 0.6f);
        private static readonly Color ZoneColor = new Color(0.95f, 0.9f, 0.75f, 0.85f);
        private static readonly Color WaystoneColor = new Color(0.3f, 0.95f, 0.9f);
        private static readonly Color UnknownWaystoneColor = new Color(0.45f, 0.5f, 0.5f);
        private static readonly Color VendorColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color PortalColor = new Color(0.75f, 0.45f, 1f);
        private static readonly Color PlayerColor = new Color32(0x8E, 0xAC, 0xD2, 0xFF);

        private GameObject window;
        private Text title;
        private RectTransform view;
        private RawImage picture;
        private readonly List<Image> dots = new List<Image>();
        private readonly List<Text> labels = new List<Text>();
        private int usedDots;
        private int usedLabels;

        private void Awake()
        {
            Image panel = UIFactory.CreateImage("World Map", transform, PanelColor);
            window = panel.gameObject;
            panel.gameObject.AddComponent<Outline>().effectColor = BorderColor;
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);

            title = UIFactory.CreateText("Title", panel.transform, 24, TextAnchor.UpperCenter);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(size.x - 100f, 32f));
            title.color = TitleColor;
            title.fontStyle = FontStyle.Bold;

            Button close = UIFactory.CreateButton("Close", panel.transform, new Color(0.4f, 0.12f, 0.1f, 0.95f), "X", 18);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(34f, 34f));
            close.onClick.AddListener(() => window.SetActive(false));

            Image background = UIFactory.CreateImage("View", panel.transform, MapPicture.OutsideColor);
            background.raycastTarget = false;
            UIFactory.Place(background.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(size.x - 32f, size.y - 64f));
            background.gameObject.AddComponent<RectMask2D>();
            view = background.rectTransform;

            picture = new GameObject("Map", typeof(RectTransform)).AddComponent<RawImage>();
            picture.transform.SetParent(view, false);
            picture.raycastTarget = false;
            picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            window.SetActive(false);
        }

        private void Update()
        {
            Health self = UIFactory.LocalPlayer<Health>();
            if (self == null || !self.IsSpawned)
            {
                if (window.activeSelf) window.SetActive(false);
                return;
            }

            var input = self.GetComponent<PlayerInputReader>();
            if (input != null && input.enabled)
            {
                if (input.MapPressed) window.SetActive(!window.activeSelf);
                else if (input.CancelPressed && window.activeSelf) window.SetActive(false);
            }
            if (window.activeSelf) Draw(self);
        }

        private void Draw(Health self)
        {
            Vector2 position = self.transform.position;
            WorldNavigation map = WorldNavigation.At(position);
            usedDots = 0;
            usedLabels = 0;
            title.text = map != null ? $"{map.ZoneName}  -  {Zone.NameAt(position)}" : Zone.NameAt(position);
            picture.enabled = map != null;

            if (map != null)
            {
                Rect bounds = map.WorldBounds;
                // The whole map, as big as fits.
                float scale = Mathf.Min(view.rect.width / Mathf.Max(1f, bounds.width), view.rect.height / Mathf.Max(1f, bounds.height));
                picture.texture = MapPicture.Of(map);
                picture.rectTransform.sizeDelta = bounds.size * scale;
                picture.rectTransform.anchoredPosition = Vector2.zero;
                Vector2 ToMap(Vector2 world) => (world - bounds.center) * scale;

                foreach (Zone zone in Zone.All)
                    if (zone != null && map.Contains(zone.transform.position))
                        Label(ToMap(zone.transform.position), zone.DisplayName, ZoneColor, 15);

                var traveller = self.GetComponent<WaystoneTraveller>();
                foreach (Waystone stone in Waystone.All)
                {
                    if (stone == null || !map.Contains(stone.transform.position)) continue;
                    bool known = traveller == null || traveller.IsDiscovered(stone.Id);
                    Dot(ToMap(stone.transform.position), known ? WaystoneColor : UnknownWaystoneColor, 12f);
                    Label(ToMap(stone.transform.position) + new Vector2(0f, -16f), known ? stone.DisplayName : "?", known ? WaystoneColor : UnknownWaystoneColor, 12);
                }
                foreach (Vendor vendor in Vendor.All)
                    if (vendor != null && map.Contains(vendor.transform.position)) Dot(ToMap(vendor.transform.position), VendorColor, 7f);
                foreach (Portal portal in Portal.Portals)
                    if (portal != null && map.Contains(portal.transform.position)) Dot(ToMap(portal.transform.position), PortalColor, 12f);
                foreach (Health unit in Health.Spawned)
                    if (unit != null && unit != self && unit.Faction == Faction.Player && map.Contains(unit.transform.position))
                        Dot(ToMap(unit.transform.position), PlayerColor, 9f);
                // You, last so you are on top.
                Dot(ToMap(position), Color.white, 14f);
                Label(ToMap(position) + new Vector2(0f, 18f), "You", Color.white, 13);
            }

            for (int i = usedDots; i < dots.Count; i++)
                if (dots[i].gameObject.activeSelf) dots[i].gameObject.SetActive(false);
            for (int i = usedLabels; i < labels.Count; i++)
                if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
        }

        private void Dot(Vector2 at, Color color, float diameter)
        {
            if (usedDots == dots.Count)
            {
                Image created = UIFactory.CreateImage("Dot", view, color);
                created.sprite = UIFactory.CircleSprite;
                created.raycastTarget = false;
                created.rectTransform.anchorMin = created.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                created.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.85f);
                dots.Add(created);
            }
            Image dot = dots[usedDots++];
            if (!dot.gameObject.activeSelf) dot.gameObject.SetActive(true);
            dot.transform.SetAsLastSibling();
            dot.color = color;
            dot.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            dot.rectTransform.anchoredPosition = at;
        }

        private void Label(Vector2 at, string text, Color color, int fontSize)
        {
            if (usedLabels == labels.Count)
            {
                Text created = UIFactory.CreateText("Label", view, fontSize, TextAnchor.MiddleCenter);
                created.rectTransform.anchorMin = created.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                created.rectTransform.sizeDelta = new Vector2(220f, 22f);
                created.horizontalOverflow = HorizontalWrapMode.Overflow;
                labels.Add(created);
            }
            Text label = labels[usedLabels++];
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            label.transform.SetAsLastSibling();
            label.text = text;
            label.color = color;
            label.fontSize = fontSize;
            label.rectTransform.anchoredPosition = at;
        }
    }
}
