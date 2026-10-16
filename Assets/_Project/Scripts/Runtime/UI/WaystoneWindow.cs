using System.Collections.Generic;
using RPG.Controls;
using RPG.Items;
using RPG.World;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// The list of waystones shown when the local player uses one: discovered stones can be travelled to for gold,
    /// undiscovered ones are greyed out. Closes on Esc, the X button, travelling, or walking away.
    /// Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class WaystoneWindow : MonoBehaviour
    {
        [SerializeField, Min(20f)] private float rowHeight = 40f;
        [SerializeField, Min(100f)] private float width = 380f;

        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.08f, 0.95f);
        private static readonly Color RowColor = new Color(0.12f, 0.16f, 0.18f, 0.95f);
        private static readonly Color HereColor = new Color(0.55f, 0.85f, 1f);
        private static readonly Color LockedColor = new Color(0.5f, 0.5f, 0.5f);
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.25f);
        private const float WalkAwayDistance = 1.5f;

        private static WaystoneWindow instance;
        private Waystone requested;

        private GameObject window;
        private Text title;
        private RectTransform list;
        private readonly List<(Button button, Text name, Text info, Waystone stone)> rows = new List<(Button, Text, Text, Waystone)>();
        private Waystone shown;

        public static void Open(Waystone stone)
        {
            if (instance != null) instance.requested = stone;
        }

        private void Awake()
        {
            instance = this;
            Image panel = UIFactory.CreateImage("Waystones", transform, PanelColor);
            window = panel.gameObject;
            panel.gameObject.AddComponent<Outline>().effectColor = new Color(0.3f, 0.6f, 0.65f, 0.9f);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(width, 200f));

            title = UIFactory.CreateText("Title", panel.transform, 22, TextAnchor.UpperLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(width - 60f, 54f));
            title.fontStyle = FontStyle.Bold;

            Button close = UIFactory.CreateButton("Close", panel.transform, new Color(0.4f, 0.12f, 0.1f, 0.95f), "X", 18);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(34f, 34f));
            close.onClick.AddListener(Hide);

            list = UIFactory.CreateRect("Stones", panel.transform);
            UIFactory.Place(list, new Vector2(0f, 1f), new Vector2(14f, -70f), new Vector2(width - 28f, 10f));

            window.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Show(Waystone stone)
        {
            shown = stone;
            var traveller = UIFactory.LocalPlayer<WaystoneTraveller>();
            int cost = traveller != null ? traveller.TravelCost : 0;
            title.text = cost > 0 ? $"{stone.DisplayName}\n<size=14>Travel to a discovered waypoint for {cost} gold.</size>"
                : $"{stone.DisplayName}\n<size=14>Teleport to a discovered waypoint.</size>";

            foreach (var row in rows) Destroy(row.button.gameObject);
            rows.Clear();
            for (int i = 0; i < Waystone.All.Count; i++)
                rows.Add(CreateRow(i, Waystone.All[i]));

            float height = 70f + rows.Count * (rowHeight + 4f) + 14f;
            ((RectTransform)window.transform).sizeDelta = new Vector2(width, height);
            window.SetActive(true);
        }

        private (Button, Text, Text, Waystone) CreateRow(int index, Waystone stone)
        {
            Button button = UIFactory.CreateButton("Stone", list, RowColor, null, 0);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, -index * (rowHeight + 4f)), new Vector2(width - 28f, rowHeight));
            button.onClick.AddListener(() => Travel(stone));

            Text name = UIFactory.CreateText("Name", button.transform, 17, TextAnchor.MiddleLeft);
            UIFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(width - 170f, rowHeight));
            Text info = UIFactory.CreateText("Info", button.transform, 15, TextAnchor.MiddleRight);
            UIFactory.Place(info.rectTransform, new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(140f, rowHeight));
            return (button, name, info, stone);
        }

        private void Travel(Waystone stone)
        {
            var traveller = UIFactory.LocalPlayer<WaystoneTraveller>();
            if (traveller == null || stone == shown || !traveller.IsDiscovered(stone.Id)) return;
            traveller.RequestTravel(stone.Id);
            Hide();
        }

        private void Hide()
        {
            window.SetActive(false);
            shown = null;
        }

        private void Update()
        {
            if (requested != null)
            {
                Show(requested);
                requested = null;
            }
            if (!window.activeSelf) return;

            var traveller = UIFactory.LocalPlayer<WaystoneTraveller>();
            PlayerInputReader input = traveller != null ? traveller.GetComponent<PlayerInputReader>() : null;
            bool walkedAway = traveller == null || shown == null
                              || Vector2.Distance(traveller.transform.position, shown.transform.position) > shown.UseRange + WalkAwayDistance;
            if (walkedAway || (input != null && input.CancelPressed))
            {
                Hide();
                return;
            }

            var inventory = traveller.GetComponent<Inventory>();
            bool canPay = traveller.TravelCost <= 0 || inventory == null || inventory.Gold >= traveller.TravelCost;
            foreach (var (button, name, info, stone) in rows)
            {
                bool here = stone == shown;
                bool known = traveller.IsDiscovered(stone.Id);
                name.text = known || here ? stone.DisplayName : "Undiscovered";
                name.color = here ? HereColor : known ? Color.white : LockedColor;
                info.text = here ? "You are here" : !known ? "" : traveller.TravelCost > 0 ? $"Travel  {traveller.TravelCost} g" : "Teleport";
                info.color = here ? HereColor : canPay ? GoldColor : new Color(1f, 0.4f, 0.3f);
                button.interactable = known && !here;
            }
        }
    }
}
