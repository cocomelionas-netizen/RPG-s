using System.Collections.Generic;
using System.Text;
using RPG.Combat;
using RPG.Controls;
using RPG.Dungeons;
using RPG.Navigation;
using RPG.Vendors;
using RPG.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The whole map of the place the local player is in, opened with M: the painted island (<see cref="MapPicture"/>)
    /// with round badges (Resources/UI/Map, Tools/Art/worldmap.py) for the town, the places with their level ranges
    /// (<see cref="ZoneMap"/> labels), the dungeon entrances, the elites and bosses, the waystones (greyed until
    /// discovered), the merchants and trainers, the townsfolk, other players, a marked place and you (an arrow the way
    /// you walk). The mouse wheel zooms in on the pointer (merchants show from 2x, townsfolk and boss names from 3x),
    /// dragging moves the map, the pointer over a badge tells what it is. Closes with M, Esc or the X. Builds its own UI;
    /// place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WorldMap : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(1300f, 800f);

        private const float MaxZoom = 8f;
        private const float MerchantZoom = 2f;
        private const float PersonZoom = 3f;
        private const float BossNameZoom = 3f;
        private const float LegendHeight = 34f;

        private static readonly Color TitleColor = new Color(1f, 0.88f, 0.6f);
        private static readonly Color PlaceColor = new Color(0.98f, 0.95f, 0.86f);
        private static readonly Color TownColor = new Color(1f, 0.9f, 0.62f);
        private static readonly Color BossColor = new Color(1f, 0.62f, 0.55f);
        private static readonly Color DungeonColor = new Color(0.86f, 0.74f, 1f);
        private static readonly Color UnknownTint = new Color(0.45f, 0.45f, 0.45f, 0.9f);
        private static readonly Color PlayerColor = new Color32(0x8E, 0xAC, 0xD2, 0xFF);

        private static readonly Dictionary<string, Sprite> Icons = new Dictionary<string, Sprite>();

        /// <summary>A badge drawn this frame, kept for the pointer's tooltip.</summary>
        private struct Shown
        {
            public Vector2 at;
            public float radius;
            public string tooltip;
        }

        private GameObject window;
        private Canvas rootCanvas;
        private Text title;
        private RectTransform view;
        private RawImage picture;
        private Image viewBackground;
        private RectTransform tooltipBox;
        private Text tooltipText;
        private string shownTooltip;
        private readonly List<Image> icons = new List<Image>();
        private readonly List<Text> labels = new List<Text>();
        private readonly List<Shown> shown = new List<Shown>();
        private int usedIcons;
        private int usedLabels;
        private int drawOrder;
        private string lastTitleZone;
        private string lastTitleMap;
        private readonly Dictionary<string, string> labelTexts = new Dictionary<string, string>();
        private readonly Dictionary<Object, string> tooltips = new Dictionary<Object, string>();

        private float zoom = 1f;
        private Vector2 pan;
        private bool dragging;
        private Vector2 lastPointer;
        private Vector2 lastPosition;
        private float heading;
        private WorldNavigation lastMap;

        /// <summary>A badge of Resources/UI/Map (Town, Dungeon, Boss, Waystone, Weapons, Armor, Potions, Gems, Food, Bank,
        /// Transmute, Trainer, Healer, Goods, Person, Place, Player, Pin), or null when it is missing.</summary>
        public static Sprite Icon(string name)
        {
            if (Icons.TryGetValue(name, out Sprite sprite)) return sprite;
            sprite = Resources.Load<Sprite>("UI/Map/" + name);
            Icons[name] = sprite;
            return sprite;
        }

        /// <summary>The badge of a merchant by what it sells (its title), or of a trainer or the transmuter.</summary>
        public static string VendorIcon(VendorDefinition vendor)
        {
            if (vendor == null) return "Goods";
            if (vendor.Transmutes) return "Transmute";
            string kind = (vendor.title ?? "").ToLowerInvariant();
            if (kind == "paladin" || kind == "ranger" || kind == "mage") return "Trainer";
            if (kind.Contains("armor")) return "Armor";
            if (kind.Contains("blade") || kind.Contains("axe") || kind.Contains("ranged") || kind.Contains("staves")) return "Weapons";
            if (kind.Contains("alchemist") || kind.Contains("witch")) return "Potions";
            if (kind.Contains("gem") || kind.Contains("jewel")) return "Gems";
            if (kind.Contains("inn") || kind.Contains("butcher")) return "Food";
            if (kind.Contains("bank") || kind.Contains("paymaster")) return "Bank";
            if (kind.Contains("cleric") || kind.Contains("healer")) return "Healer";
            return "Goods";
        }

        /// <summary>A dungeon's tooltip: its name, who may enter and its bosses.</summary>
        public static string DungeonTooltip(DungeonDefinition dungeon, string fallbackName)
        {
            if (dungeon == null) return fallbackName;
            var text = new StringBuilder(dungeon.displayName);
            text.Append($"\nDungeon: level {dungeon.minLevel}+, up to {dungeon.maxPlayers} player{(dungeon.maxPlayers == 1 ? "" : "s")}");
            if (dungeon.key != null) text.Append($"\nNeeds {dungeon.key.displayName}");
            var bosses = new List<string>();
            foreach (DungeonMonsterSpot spot in dungeon.monsters)
            {
                if (!spot.boss || string.IsNullOrEmpty(spot.displayName)) continue;
                string boss = $"{spot.displayName} ({spot.level})";
                if (!bosses.Contains(boss)) bosses.Add(boss);
            }
            if (bosses.Count > 0) text.Append("\nBoss" + (bosses.Count > 1 ? "es: " : ": ") + string.Join(", ", bosses));
            return text.ToString();
        }

        private void Awake()
        {
            rootCanvas = GetComponentInParent<Canvas>();
            Image panel = UIFactory.CreateImage("World Map", transform, new Color(0.06f, 0.05f, 0.04f, 0.96f));
            window = panel.gameObject;
            window.AddComponent<OpenWindow>();
            UIFactory.AddFrame(panel);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), Vector2.zero, size);

            title = UIFactory.CreateText("Title", panel.transform, 24, TextAnchor.UpperCenter);
            UIFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(size.x - 260f, 32f));
            title.color = TitleColor;
            title.fontStyle = FontStyle.Bold;

            Button close = UIFactory.CreateButton("Close", panel.transform, new Color(0.4f, 0.12f, 0.1f, 0.95f), "X", 18);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-14f, -12f), new Vector2(34f, 34f));
            close.onClick.AddListener(() => window.SetActive(false));
            Button zoomIn = UIFactory.CreateButton("Zoom In", panel.transform, new Color(0.25f, 0.3f, 0.2f, 0.95f), "+", 18);
            UIFactory.Place((RectTransform)zoomIn.transform, new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(34f, 34f));
            zoomIn.onClick.AddListener(() => ZoomAt(Vector2.zero, 1.5f));
            Button zoomOut = UIFactory.CreateButton("Zoom Out", panel.transform, new Color(0.25f, 0.3f, 0.2f, 0.95f), "-", 18);
            UIFactory.Place((RectTransform)zoomOut.transform, new Vector2(0f, 1f), new Vector2(58f, -12f), new Vector2(34f, 34f));
            zoomOut.onClick.AddListener(() => ZoomAt(Vector2.zero, 1f / 1.5f));
            Button me = UIFactory.CreateButton("Find Me", panel.transform, new Color(0.25f, 0.3f, 0.2f, 0.95f), "Find Me", 15);
            UIFactory.Place((RectTransform)me.transform, new Vector2(0f, 1f), new Vector2(98f, -12f), new Vector2(96f, 34f));
            me.onClick.AddListener(CenterOnPlayer);

            viewBackground = UIFactory.CreateImage("View", panel.transform, MapPicture.OutsideColor);
            // Catches the pointer so clicks, drags and the wheel on the map do not walk or zoom the camera.
            viewBackground.raycastTarget = true;
            UIFactory.Place(viewBackground.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 18f + LegendHeight),
                new Vector2(size.x - 36f, size.y - 76f - LegendHeight));
            viewBackground.gameObject.AddComponent<RectMask2D>();
            // The badges move every frame while the map is open: a canvas of their own keeps the rest of the HUD as it is.
            viewBackground.gameObject.AddComponent<Canvas>();
            viewBackground.gameObject.AddComponent<GraphicRaycaster>();
            view = viewBackground.rectTransform;

            picture = new GameObject("Map", typeof(RectTransform)).AddComponent<RawImage>();
            picture.transform.SetParent(view, false);
            picture.raycastTarget = false;
            picture.rectTransform.anchorMin = picture.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);

            BuildLegend(panel.rectTransform);
            // On the full-screen layer, so it is placed in screen units like the other tooltips.
            BuildTooltip(transform);
            window.SetActive(false);
        }

        private void BuildLegend(RectTransform panel)
        {
            RectTransform legend = UIFactory.CreateRect("Legend", panel);
            UIFactory.Place(legend, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(size.x - 40f, LegendHeight));
            float x = 0f;
            foreach ((string icon, string text) in new[]
            {
                ("Town", "Town"), ("Place", "Area"), ("Dungeon", "Dungeon"), ("Boss", "Elite / boss"), ("Waystone", "Waystone"),
                ("Goods", "Merchant"), ("Trainer", "Trainer"), ("Person", "Townsfolk"), ("Player", "You"),
            })
            {
                Image badge = UIFactory.CreateImage("Icon", legend, Color.white);
                badge.raycastTarget = false;
                badge.sprite = Icon(icon) != null ? Icon(icon) : UIFactory.CircleSprite;
                UIFactory.Place(badge.rectTransform, new Vector2(0f, 0.5f), new Vector2(x, 0f), new Vector2(24f, 24f));
                Text label = UIFactory.CreateText("Text", legend, 14, TextAnchor.MiddleLeft);
                label.text = text;
                label.color = PlaceColor;
                float width = Mathf.Ceil(label.preferredWidth) + 4f;
                UIFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(x + 28f, 0f), new Vector2(width, 24f));
                x += 28f + width + 16f;
            }
            Text hint = UIFactory.CreateText("Hint", legend, 13, TextAnchor.MiddleRight);
            hint.text = "Wheel: zoom   Drag: move";
            hint.color = new Color(0.75f, 0.72f, 0.64f);
            UIFactory.Place(hint.rectTransform, new Vector2(1f, 0.5f), Vector2.zero, new Vector2(220f, 24f));
        }

        private void BuildTooltip(Transform parent)
        {
            Image box = UIFactory.CreateImage("Map Tooltip", parent, new Color(0.07f, 0.09f, 0.12f, 0.94f));
            box.raycastTarget = false;
            UIFactory.StyleBox(box);
            UIFactory.MakeTopmost(box.gameObject);
            tooltipBox = box.rectTransform;
            tooltipBox.anchorMin = tooltipBox.anchorMax = Vector2.zero;
            tooltipBox.pivot = new Vector2(0f, 1f);
            tooltipText = UIFactory.CreateText("Text", box.transform, 15, TextAnchor.UpperLeft);
            tooltipText.supportRichText = false;
            tooltipText.color = PlaceColor;
            tooltipText.rectTransform.anchorMin = tooltipText.rectTransform.anchorMax = tooltipText.rectTransform.pivot = new Vector2(0f, 1f);
            tooltipText.rectTransform.anchoredPosition = new Vector2(10f, -8f);
            box.gameObject.SetActive(false);
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
                else if (OpenWindow.EscapeCloses(input) && window.activeSelf) window.SetActive(false);
            }
            if (window.activeSelf)
            {
                HandlePointer();
                Draw(self);
            }
            else
            {
                dragging = false;
                if (tooltipBox.gameObject.activeSelf) tooltipBox.gameObject.SetActive(false);
            }
        }

        /// <summary>The pointer in the view's units, from its middle; false when it is outside the view.</summary>
        private bool PointerInView(out Vector2 local)
        {
            local = Vector2.zero;
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;
            Camera eventCamera = rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? rootCanvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(view, mouse.position.ReadValue(), eventCamera, out local)) return false;
            local -= view.rect.center;
            return view.rect.Contains(local + view.rect.center);
        }

        private void HandlePointer()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;
            bool inside = PointerInView(out Vector2 pointer);
            float wheel = mouse.scroll.ReadValue().y;
            if (inside && Mathf.Abs(wheel) > 0.01f) ZoomAt(pointer, wheel > 0f ? 1.25f : 0.8f);

            if (mouse.leftButton.wasPressedThisFrame && inside)
            {
                dragging = true;
                lastPointer = mouse.position.ReadValue();
            }
            if (!mouse.leftButton.isPressed) dragging = false;
            if (dragging)
            {
                Vector2 now = mouse.position.ReadValue();
                float scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
                pan += (now - lastPointer) / Mathf.Max(0.0001f, scale);
                lastPointer = now;
                ClampPan();
            }
        }

        /// <summary>Zooms by <paramref name="factor"/> keeping the map point under <paramref name="at"/> (view units) where it is.</summary>
        private void ZoomAt(Vector2 at, float factor)
        {
            float next = Mathf.Clamp(zoom * factor, 1f, MaxZoom);
            pan = at - (at - pan) * (next / zoom);
            zoom = next;
            ClampPan();
        }

        private void CenterOnPlayer()
        {
            Health self = UIFactory.LocalPlayer<Health>();
            WorldNavigation map = self != null ? WorldNavigation.At(self.transform.position) : null;
            if (map == null) return;
            if (zoom < 3f) zoom = 3f;
            Rect bounds = map.WorldBounds;
            pan = -((Vector2)self.transform.position - bounds.center) * FitScale(bounds) * zoom;
            ClampPan();
        }

        private float FitScale(Rect bounds) =>
            Mathf.Min(view.rect.width / Mathf.Max(1f, bounds.width), view.rect.height / Mathf.Max(1f, bounds.height));

        /// <summary>Keeps the map over the view: it may not be dragged further than its edges.</summary>
        private void ClampPan()
        {
            if (lastMap == null) return;
            Vector2 mapSize = lastMap.WorldBounds.size * FitScale(lastMap.WorldBounds) * zoom;
            Vector2 room = Vector2.Max(Vector2.zero, (mapSize - view.rect.size) * 0.5f);
            pan = new Vector2(Mathf.Clamp(pan.x, -room.x, room.x), Mathf.Clamp(pan.y, -room.y, room.y));
        }

        private void Draw(Health self)
        {
            Vector2 position = self.transform.position;
            WorldNavigation map = WorldNavigation.At(position);
            if (map != lastMap)
            {
                lastMap = map;
                zoom = 1f;
                pan = Vector2.zero;
            }
            usedIcons = 0;
            usedLabels = 0;
            drawOrder = 0;
            shown.Clear();
            string zone = Zone.NameAt(position);
            string mapName = map != null ? map.ZoneName : null;
            if (zone != lastTitleZone || mapName != lastTitleMap)
            {
                lastTitleZone = zone;
                lastTitleMap = mapName;
                title.text = map != null ? $"{map.ZoneName}  -  {zone}" : zone;
            }
            picture.enabled = map != null;
            Color around = MapPicture.BackgroundOf(map);
            if (viewBackground.color != around) viewBackground.color = around;

            if ((position - lastPosition).sqrMagnitude > 0.0004f)
            {
                Vector2 step = position - lastPosition;
                heading = Mathf.Atan2(step.y, step.x) * Mathf.Rad2Deg - 90f;
                lastPosition = position;
            }

            if (map != null)
            {
                Rect bounds = map.WorldBounds;
                float scale = FitScale(bounds) * zoom;
                picture.texture = MapPicture.Of(map);
                picture.rectTransform.sizeDelta = bounds.size * scale;
                picture.rectTransform.anchoredPosition = pan;
                Vector2 ToMap(Vector2 world) => (world - bounds.center) * scale + pan;
                Rect visible = view.rect;
                visible.center = Vector2.zero;
                bool Visible(Vector2 at) => at.x > visible.xMin - 30f && at.x < visible.xMax + 30f && at.y > visible.yMin - 30f && at.y < visible.yMax + 30f;

                // The places with their level ranges, the town in large letters, the elites and bosses.
                foreach (ZoneMap zoneMap in ZoneMap.All)
                {
                    if (zoneMap == null) continue;
                    foreach (ZoneMap.Label label in zoneMap.Labels)
                    {
                        if (!map.Contains(label.position)) continue;
                        Vector2 at = ToMap(label.position);
                        if (!Visible(at)) continue;
                        if (label.town)
                        {
                            Badge(at, "Town", 40f, Color.white, label.name);
                            Label(at + new Vector2(0f, 34f), label.name, TownColor, 26);
                            continue;
                        }
                        if (!labelTexts.TryGetValue(label.name + label.detail, out string text))
                            labelTexts[label.name + label.detail] = text = string.IsNullOrEmpty(label.detail) ? label.name : $"{label.name}\n{label.detail}";
                        if (label.boss)
                        {
                            Badge(at, "Boss", 26f, Color.white, text);
                            if (zoom >= BossNameZoom) Label(at + new Vector2(0f, 22f), label.name, BossColor, 12);
                            continue;
                        }
                        Badge(at, "Place", 22f, Color.white, text);
                        Label(at + new Vector2(0f, 30f), text, PlaceColor, 14);
                    }
                }

                foreach (Portal portal in Portal.Portals)
                {
                    if (portal == null || !map.Contains(portal.transform.position)) continue;
                    Vector2 at = ToMap(portal.transform.position);
                    if (!Visible(at)) continue;
                    if (!tooltips.TryGetValue(portal, out string tip))
                        tooltips[portal] = tip = portal.Kind == PortalKind.DungeonExit
                            ? $"Way out\n{portal.Label}"
                            : DungeonTooltip(portal.Dungeon, portal.Label);
                    Badge(at, "Dungeon", 30f, Color.white, tip);
                    if (portal.Kind == PortalKind.DungeonEntrance && portal.Dungeon != null)
                        Label(at + new Vector2(0f, -26f), portal.Dungeon.displayName, DungeonColor, 13);
                }

                var traveller = self.GetComponent<WaystoneTraveller>();
                foreach (Waystone stone in Waystone.All)
                {
                    if (stone == null || !map.Contains(stone.transform.position)) continue;
                    Vector2 at = ToMap(stone.transform.position);
                    if (!Visible(at)) continue;
                    bool known = traveller == null || traveller.IsDiscovered(stone.Id);
                    if (!tooltips.TryGetValue(stone, out string tip)) tooltips[stone] = tip = $"{stone.DisplayName}\nWaystone";
                    Badge(at, "Waystone", 24f, known ? Color.white : UnknownTint, known ? tip : tip + " (not found yet)");
                }

                if (zoom >= MerchantZoom)
                    foreach (Vendor vendor in Vendor.All)
                    {
                        if (vendor == null || !map.Contains(vendor.transform.position)) continue;
                        Vector2 at = ToMap(vendor.transform.position);
                        if (!Visible(at)) continue;
                        if (!tooltips.TryGetValue(vendor, out string tip))
                        {
                            string kind = vendor.Definition != null ? vendor.Definition.title : "";
                            tooltips[vendor] = tip = string.IsNullOrEmpty(kind) ? vendor.DisplayName : $"{vendor.DisplayName}\n{kind}";
                        }
                        Badge(at, VendorIcon(vendor.Definition), 24f, Color.white, tip);
                    }

                if (zoom >= PersonZoom)
                    foreach (Townsperson person in Townsperson.All)
                    {
                        if (person == null || !map.Contains(person.transform.position)) continue;
                        Vector2 at = ToMap(person.transform.position);
                        if (!Visible(at)) continue;
                        if (!tooltips.TryGetValue(person, out string tip))
                            tooltips[person] = tip = string.IsNullOrEmpty(person.Title) ? person.DisplayName : $"{person.DisplayName}\n{person.Title}";
                        Badge(at, "Person", 20f, Color.white, tip);
                    }

                foreach (Health unit in Health.Spawned)
                    if (unit != null && unit != self && unit.Faction == Faction.Player && map.Contains(unit.transform.position))
                    {
                        Vector2 at = ToMap(unit.transform.position);
                        if (Visible(at)) Dot(at, PlayerColor, 12f, "Another player");
                    }
                if (MapPin.Active && map.Contains(MapPin.Position))
                {
                    Vector2 at = ToMap(MapPin.Position);
                    Badge(at + new Vector2(0f, 12f), "Pin", 28f, Color.white, MapPin.Label);
                    Label(at + new Vector2(0f, 34f), MapPin.Label, MapPin.PinColor, 13);
                }
                // You, last so you are on top: an arrow the way you walk.
                Image arrow = Badge(ToMap(position), "Player", 30f, Color.white, "You");
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, heading);
            }

            for (int i = usedIcons; i < icons.Count; i++)
                if (icons[i].gameObject.activeSelf) icons[i].gameObject.SetActive(false);
            for (int i = usedLabels; i < labels.Count; i++)
                if (labels[i].gameObject.activeSelf) labels[i].gameObject.SetActive(false);
            ShowTooltip();
        }

        /// <summary>The tooltip of the badge under the pointer (the nearest one when several overlap).</summary>
        private void ShowTooltip()
        {
            string tip = null;
            if (!dragging && PointerInView(out Vector2 pointer))
            {
                float best = float.MaxValue;
                foreach (Shown badge in shown)
                {
                    float distance = (badge.at - pointer).sqrMagnitude;
                    if (distance <= badge.radius * badge.radius && distance < best)
                    {
                        best = distance;
                        tip = badge.tooltip;
                    }
                }
            }
            if (string.IsNullOrEmpty(tip))
            {
                if (tooltipBox.gameObject.activeSelf) tooltipBox.gameObject.SetActive(false);
                shownTooltip = null;
                return;
            }
            if (!ReferenceEquals(tip, shownTooltip))
            {
                shownTooltip = tip;
                tooltipText.text = tip;
                tooltipText.rectTransform.sizeDelta = new Vector2(360f, 40f);
                float width = Mathf.Min(360f, Mathf.Ceil(tooltipText.preferredWidth) + 2f);
                tooltipText.rectTransform.sizeDelta = new Vector2(width, 40f);
                float height = Mathf.Ceil(tooltipText.preferredHeight);
                tooltipText.rectTransform.sizeDelta = new Vector2(width, height);
                tooltipBox.sizeDelta = new Vector2(width + 20f, height + 16f);
            }
            if (!tooltipBox.gameObject.activeSelf) tooltipBox.gameObject.SetActive(true);
            float scale = rootCanvas != null ? rootCanvas.scaleFactor : 1f;
            Vector2 mouse = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
            tooltipBox.anchoredPosition = UIFactory.KeepOnScreen(tooltipBox, mouse / scale + new Vector2(18f, -18f), scale);
        }

        private Image Badge(Vector2 at, string icon, float diameter, Color tint, string tooltip)
        {
            Sprite sprite = Icon(icon);
            Image badge = NextIcon();
            badge.sprite = sprite != null ? sprite : UIFactory.CircleSprite;
            badge.color = sprite != null ? tint : new Color(0.8f * tint.r, 0.7f * tint.g, 0.4f * tint.b, tint.a);
            badge.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            badge.rectTransform.anchoredPosition = at;
            badge.rectTransform.localRotation = Quaternion.identity;
            if (!string.IsNullOrEmpty(tooltip)) shown.Add(new Shown { at = at, radius = diameter * 0.5f + 2f, tooltip = tooltip });
            return badge;
        }

        private void Dot(Vector2 at, Color color, float diameter, string tooltip)
        {
            Image dot = NextIcon();
            dot.sprite = UIFactory.CircleSprite;
            dot.color = color;
            dot.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            dot.rectTransform.anchoredPosition = at;
            dot.rectTransform.localRotation = Quaternion.identity;
            if (!string.IsNullOrEmpty(tooltip)) shown.Add(new Shown { at = at, radius = diameter * 0.5f + 3f, tooltip = tooltip });
        }

        private Image NextIcon()
        {
            if (usedIcons == icons.Count)
            {
                Image created = UIFactory.CreateImage("Badge", view, Color.white);
                created.raycastTarget = false;
                created.preserveAspect = true;
                created.rectTransform.anchorMin = created.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                icons.Add(created);
            }
            Image icon = icons[usedIcons++];
            if (!icon.gameObject.activeSelf) icon.gameObject.SetActive(true);
            KeepOrder(icon.transform);
            return icon;
        }

        /// <summary>Draws the elements in the order they are used (after the map picture), moving one only when it is out of place.</summary>
        private void KeepOrder(Transform element)
        {
            int wanted = Mathf.Min(picture.transform.GetSiblingIndex() + 1 + drawOrder++, view.childCount - 1);
            if (element.GetSiblingIndex() != wanted) element.SetSiblingIndex(wanted);
        }

        private void Label(Vector2 at, string text, Color color, int fontSize)
        {
            if (usedLabels == labels.Count)
            {
                Text created = UIFactory.CreateText("Label", view, fontSize, TextAnchor.MiddleCenter);
                created.raycastTarget = false;
                created.rectTransform.anchorMin = created.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                created.rectTransform.sizeDelta = new Vector2(260f, 40f);
                created.horizontalOverflow = HorizontalWrapMode.Overflow;
                created.verticalOverflow = VerticalWrapMode.Overflow;
                created.fontStyle = FontStyle.Bold;
                created.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.9f);
                created.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -2f);
                labels.Add(created);
            }
            Text label = labels[usedLabels++];
            if (!label.gameObject.activeSelf) label.gameObject.SetActive(true);
            KeepOrder(label.transform);
            label.text = text;
            label.color = color;
            label.fontSize = fontSize;
            label.rectTransform.anchoredPosition = at;
        }
    }
}
