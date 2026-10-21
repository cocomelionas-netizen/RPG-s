using System.Collections.Generic;
using RPG.Controls;
using RPG.Player;
using RPG.World;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// F9: the cheats window for testing (<see cref="Cheats"/>): levels, experience, gold, healing, god mode, speed, every
    /// spell and waypoint, killing the monsters around, items, and teleporting to any waypoint. Only for the host's own
    /// character (offline single player). Closes with F9, Esc or the X. Builds its own UI; place it on a RectTransform
    /// that fills the HUD canvas.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CheatWindow : MonoBehaviour
    {
        private const float Width = 420f;
        private const float ButtonHeight = 32f;
        private const float Gap = 6f;
        private static readonly Color PanelColor = new Color(0.07f, 0.05f, 0.08f, 0.95f);
        private static readonly Color ButtonColor = new Color(0.3f, 0.14f, 0.3f, 0.95f);
        private static readonly Color TeleportColor = new Color(0.12f, 0.22f, 0.26f, 0.95f);

        private GameObject window;
        private RectTransform content;
        private Text godLabel;
        private Text fastLabel;
        private float y;
        private int teleportsShown = -1;
        private readonly List<GameObject> teleportRows = new List<GameObject>();

        private void Awake()
        {
            Image panel = UIFactory.CreateImage("Cheats", transform, PanelColor);
            window = panel.gameObject;
            window.AddComponent<OpenWindow>();
            UIFactory.AddFrame(panel);
            UIFactory.Place(panel.rectTransform, new Vector2(1f, 0.5f), new Vector2(-240f, 0f), new Vector2(Width, 600f));

            Text title = UIFactory.CreateText("Title", panel.transform, 22, TextAnchor.UpperLeft);
            UIFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(14f, -10f), new Vector2(Width - 60f, 30f));
            title.fontStyle = FontStyle.Bold;
            title.text = "CHEATS (F9)";

            Button close = UIFactory.CreateButton("Close", panel.transform, new Color(0.4f, 0.12f, 0.1f, 0.95f), "X", 18);
            UIFactory.Place((RectTransform)close.transform, new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(34f, 34f));
            close.onClick.AddListener(() => window.SetActive(false));

            content = UIFactory.CreateRect("Content", panel.transform);
            UIFactory.Place(content, new Vector2(0f, 1f), new Vector2(14f, -50f), new Vector2(Width - 28f, 10f));

            Row(("Level +1", CheatCommand.LevelUp), ("Level 25", CheatCommand.MaxLevel), ($"+{Cheats.ExperienceGift} XP", CheatCommand.Experience));
            Row(($"+{Cheats.GoldGift} gold", CheatCommand.Gold), ("Heal + mana", CheatCommand.Heal), ("Kill around", CheatCommand.KillNearby));
            Text[] toggles = Row(("God mode", CheatCommand.God), ("Fast", CheatCommand.Speed), ("All spells", CheatCommand.LearnSpells));
            godLabel = toggles[0];
            fastLabel = toggles[1];
            Row(("Potions, food", CheatCommand.Potions), ("Gems", CheatCommand.Gems), ("Orbs, keys, scrolls", CheatCommand.Orbs));
            Row(("Gear of my level", CheatCommand.Gear), ("Empty the bag", CheatCommand.ClearBag), ("All waypoints", CheatCommand.DiscoverWaystones));

            Text teleport = UIFactory.CreateText("Teleport", content, 16, TextAnchor.LowerLeft);
            UIFactory.Place(teleport.rectTransform, new Vector2(0f, 1f), new Vector2(0f, -y), new Vector2(Width - 28f, 24f));
            teleport.text = "Teleport to:";
            y += 28f;

            window.SetActive(false);
        }

        /// <summary>A row of three cheat buttons; returns their labels.</summary>
        private Text[] Row(params (string label, CheatCommand command)[] buttons)
        {
            float width = (Width - 28f - Gap * (buttons.Length - 1)) / buttons.Length;
            var labels = new Text[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                CheatCommand command = buttons[i].command;
                Button button = UIFactory.CreateButton(buttons[i].label, content, ButtonColor, buttons[i].label, 14);
                UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f), new Vector2(i * (width + Gap), -y), new Vector2(width, ButtonHeight));
                button.onClick.AddListener(() => Send(command));
                labels[i] = button.GetComponentInChildren<Text>();
            }
            y += ButtonHeight + Gap;
            return labels;
        }

        private static void Send(CheatCommand command, int value = 0)
        {
            var cheats = UIFactory.LocalPlayer<Cheats>();
            if (cheats != null) cheats.Request(command, value);
        }

        /// <summary>A button per waypoint (two per row), made again when the waypoints change.</summary>
        private void DrawTeleports()
        {
            if (teleportsShown == Waystone.All.Count) return;
            foreach (GameObject row in teleportRows) Destroy(row);
            teleportRows.Clear();
            teleportsShown = Waystone.All.Count;

            float width = (Width - 28f - Gap) / 2f;
            float top = y;
            for (int i = 0; i < Waystone.All.Count; i++)
            {
                Waystone stone = Waystone.All[i];
                int id = stone.Id;
                Button button = UIFactory.CreateButton("Waypoint", content, TeleportColor, stone.DisplayName, 13);
                UIFactory.Place((RectTransform)button.transform, new Vector2(0f, 1f),
                    new Vector2(i % 2 * (width + Gap), -(top + i / 2 * (28f + 4f))), new Vector2(width, 28f));
                button.onClick.AddListener(() => Send(CheatCommand.Teleport, id));
                teleportRows.Add(button.gameObject);
            }
            float height = 50f + top + (Waystone.All.Count + 1) / 2 * 32f + 14f;
            ((RectTransform)window.transform).sizeDelta = new Vector2(Width, height);
        }

        private void Update()
        {
            var cheats = UIFactory.LocalPlayer<Cheats>();
            PlayerInputReader input = cheats != null ? cheats.GetComponent<PlayerInputReader>() : null;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame && cheats != null)
            {
                if (!cheats.Allowed) MessageLog.Add("Cheats work only offline (or for the host's own character).", MessageLog.WarningColor);
                else window.SetActive(!window.activeSelf);
            }
            if (!window.activeSelf) return;
            if (cheats == null || !cheats.Allowed || OpenWindow.EscapeCloses(input))
            {
                window.SetActive(false);
                return;
            }

            DrawTeleports();
            // The host's character is the server's: its switches can be read directly.
            string god = cheats.God ? "God mode: ON" : "God mode";
            if (godLabel.text != god) godLabel.text = god;
            string fast = cheats.Fast ? "Fast: ON" : "Fast";
            if (fastLabel.text != fast) fastLabel.text = fast;
        }
    }
}
