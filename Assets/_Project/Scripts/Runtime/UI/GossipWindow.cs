using System.Collections.Generic;
using RPG.Controls;
using RPG.Vendors;
using RPG.World;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// What a townsperson says when the local player talks to it (the source game's gossip window): its name, the text
    /// of its menu and the replies, each opening another menu or running a script; "Goodbye" closes it. A reply that
    /// points the way to an NPC marks it on the maps (<see cref="MapPin"/>). Closes on Esc, the X or walking away.
    /// Builds its own UI; place it on a RectTransform that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class GossipWindow : MonoBehaviour
    {
        [SerializeField, Min(200f)] private float width = 460f;

        private static readonly Color PanelColor = new Color(0.06f, 0.07f, 0.08f, 0.95f);
        private static readonly Color OptionColor = new Color(0.14f, 0.12f, 0.08f, 0.95f);
        private static readonly Color OptionTextColor = new Color(1f, 0.85f, 0.45f);
        private static readonly Color TitleColor = new Color(0.8f, 0.75f, 0.65f);
        private static readonly Color BodyColor = new Color(0.93f, 0.9f, 0.82f);
        private static readonly Color NarrationColor = new Color(0.72f, 0.68f, 0.6f);
        // On parchment: dark ink.
        private static readonly Color InkColor = new Color(0.17f, 0.1f, 0.04f);
        private static readonly Color InkTitleColor = new Color(0.38f, 0.22f, 0.08f);
        private static readonly Color InkNarrationColor = new Color(0.36f, 0.26f, 0.15f);
        private Color subtitleColor = TitleColor;
        private Color narrationColor = NarrationColor;
        private const float WalkAwayDistance = 1.5f;
        private const float Padding = 16f;
        private const float OptionHeight = 34f;

        private static GossipWindow instance;
        private Townsperson requested;

        private GameObject window;
        private Text title;
        private Text body;
        private RectTransform optionList;
        private readonly List<Button> optionButtons = new List<Button>();
        private readonly List<GossipOption> options = new List<GossipOption>();
        private Townsperson shown;
        private int menu;

        /// <summary>The townsperson the window shows (null when closed).</summary>
        public static Townsperson Current => instance != null && instance.window.activeSelf ? instance.shown : null;

        public static void Open(Townsperson person)
        {
            if (instance != null) instance.requested = person;
        }

        private void Awake()
        {
            instance = this;
            Image panel = UIFactory.CreateImage("Gossip", transform, PanelColor);
            window = panel.gameObject;
            window.AddComponent<OpenWindow>();
            bool parchment = UIFactory.AddParchment(panel);
            UIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(-260f, 40f), new Vector2(width, 300f));

            title = UIFactory.CreateText("Title", panel.transform, 22, TextAnchor.UpperLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -10f), new Vector2(width - 60f, 54f));
            title.fontStyle = FontStyle.Bold;

            Button close = UIFactory.CreateButton("Close", panel.transform, new Color(0.4f, 0.12f, 0.1f, 0.95f), "X", 18);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(34f, 34f));
            close.onClick.AddListener(Hide);

            body = UIFactory.CreateText("Text", panel.transform, 16, TextAnchor.UpperLeft);
            UIFactory.Place(body.rectTransform, new Vector2(0f, 1f), new Vector2(Padding, -70f), new Vector2(width - Padding * 2f, 40f));
            body.color = BodyColor;
            if (parchment)
            {
                // Dark ink on the parchment, without the light texts' black outline.
                title.color = InkColor;
                body.color = InkColor;
                title.GetComponent<Outline>().enabled = false;
                body.GetComponent<Outline>().enabled = false;
                subtitleColor = InkTitleColor;
                narrationColor = InkNarrationColor;
            }

            optionList = UIFactory.CreateRect("Replies", panel.transform);
            UIFactory.Place(optionList, new Vector2(0f, 1f), new Vector2(Padding, -120f), new Vector2(width - Padding * 2f, 10f));

            window.SetActive(false);
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void Show(Townsperson person, int menuId)
        {
            shown = person;
            menu = menuId;
            GameObject player = LocalPlayerObject();
            title.text = string.IsNullOrEmpty(person.Title)
                ? person.DisplayName
                : $"{person.DisplayName}\n<size=14><color=#{ColorUtility.ToHtmlStringRGB(subtitleColor)}>{person.Title}</color></size>";

            GossipCatalog catalog = person.Gossip;
            GossipText text = catalog.TextFor(menu, player);
            body.text = Narration(GossipCatalog.Format(text != null ? text.text : "...", player, person.DisplayName));
            float bodyHeight = Mathf.Ceil(body.preferredHeight) + 4f;
            body.rectTransform.sizeDelta = new Vector2(width - Padding * 2f, bodyHeight);

            catalog.OptionsFor(menu, player, options);
            foreach (Button button in optionButtons) Destroy(button.gameObject);
            optionButtons.Clear();
            float y = 0f;
            for (int i = 0; i <= options.Count; i++)
            {
                GossipOption option = i < options.Count ? options[i] : null;
                string label = option != null ? GossipCatalog.Format(option.text, player, person.DisplayName) : "Goodbye.";
                Button button = CreateOption(label, y, out float height);
                if (option != null) button.onClick.AddListener(() => Choose(option));
                else button.onClick.AddListener(Hide);
                optionButtons.Add(button);
                y += height + 4f;
            }

            float top = 70f + bodyHeight + 12f;
            optionList.anchoredPosition = new Vector2(Padding, -top);
            ((RectTransform)window.transform).sizeDelta = new Vector2(width, top + y + Padding);
            window.SetActive(true);
        }

        private Button CreateOption(string label, float y, out float height)
        {
            Button button = UIFactory.CreateButton("Reply", optionList, OptionColor, null, 0);
            UIFactory.StyleBox((Image)button.targetGraphic);
            Text text = UIFactory.CreateText("Label", button.transform, 15, TextAnchor.MiddleLeft);
            text.color = OptionTextColor;
            text.supportRichText = false;
            text.text = label;
            float textWidth = width - Padding * 2f - 20f;
            UIFactory.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(textWidth, OptionHeight));
            height = Mathf.Max(OptionHeight, Mathf.Ceil(text.preferredHeight) + 12f);
            text.rectTransform.sizeDelta = new Vector2(textWidth, height);
            UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(width - Padding * 2f, height));
            return button;
        }

        private void Choose(GossipOption option)
        {
            Townsperson person = shown;
            if (person == null) return;
            var conversation = UIFactory.LocalPlayer<Conversation>();
            if (conversation != null) conversation.RequestChoose(person, option);
            PointTheWay(person.Gossip, option.script);

            if (option.nextMenu > 0) Show(person, option.nextMenu);
            else Hide();
        }

        /// <summary>A reply's "locate NPC" steps: the NPC is marked on the maps and named in the messages.</summary>
        private static void PointTheWay(GossipCatalog catalog, int script)
        {
            var steps = new List<GossipScriptStep>();
            catalog.StepsOf(script, steps);
            foreach (GossipScriptStep step in steps)
            {
                if (step.command != GossipScriptStep.LocateNpc) continue;
                Transform player = UIFactory.LocalPlayer<Transform>();
                Vector2 from = player != null ? (Vector2)player.position : Vector2.zero;
                Transform target = null;
                string label = step.name;
                Townsperson person = Townsperson.Find(step.name, from);
                if (person != null)
                {
                    target = person.transform;
                    if (!string.IsNullOrEmpty(person.Title)) label = $"{person.DisplayName} ({person.Title})";
                }
                else
                {
                    foreach (Vendor vendor in Vendor.All)
                    {
                        if (vendor.DisplayName != step.name) continue;
                        target = vendor.transform;
                        string vendorTitle = vendor.Definition != null ? vendor.Definition.title : "";
                        if (!string.IsNullOrEmpty(vendorTitle)) label = $"{vendor.DisplayName} ({vendorTitle})";
                        break;
                    }
                }
                if (target == null)
                {
                    MessageLog.Add($"{step.name} is nowhere to be found.", MessageLog.WarningColor);
                    continue;
                }
                MapPin.Set(target.position, label);
                MessageLog.Add($"{label} is marked on your map ({Heading((Vector2)target.position - from)}).", OptionTextColor);
            }
        }

        /// <summary>The source game's narration (&lt;Aldric hesitates.&gt;) in grey italics.</summary>
        private string Narration(string text) =>
            System.Text.RegularExpressions.Regex.Replace(text, "<([^<>]*)>", $"<color=#{ColorUtility.ToHtmlStringRGB(narrationColor)}><i>$1</i></color>");

        /// <summary>The way to go, as on a compass (north is up on screen).</summary>
        private static string Heading(Vector2 offset)
        {
            if (offset.magnitude < 1f) return "right here";
            string[] names = { "east", "north-east", "north", "north-west", "west", "south-west", "south", "south-east" };
            float angle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            int index = Mathf.RoundToInt(((angle % 360f) + 360f) % 360f / 45f) % 8;
            return names[index];
        }

        private void Hide()
        {
            window.SetActive(false);
            shown = null;
        }

        private static GameObject LocalPlayerObject()
        {
            Transform player = UIFactory.LocalPlayer<Transform>();
            return player != null ? player.gameObject : null;
        }

        private void Update()
        {
            if (requested != null)
            {
                if (requested.CanTalk) Show(requested, requested.GossipMenu);
                requested = null;
            }
            if (!window.activeSelf) return;

            Transform player = UIFactory.LocalPlayer<Transform>();
            PlayerInputReader input = player != null ? player.GetComponent<PlayerInputReader>() : null;
            bool walkedAway = player == null || shown == null
                              || Vector2.Distance(player.position, shown.transform.position) > shown.TalkRange + WalkAwayDistance;
            if (walkedAway || OpenWindow.EscapeCloses(input)) Hide();
        }
    }
}
