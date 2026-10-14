using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// Floating damage numbers drawn on the screen canvas above where the hit landed.
    /// White: damage you deal, yellow and bigger: critical, red: damage you take.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CombatTextLayer : MonoBehaviour
    {
        [SerializeField] private Font font;
        [SerializeField, Min(0.1f)] private float lifetime = 0.9f;
        [Tooltip("How far a number rises over its lifetime, in canvas pixels.")]
        [SerializeField] private float rise = 70f;
        [SerializeField] private int fontSize = 30;

        private static CombatTextLayer instance;

        private class Entry
        {
            public Text Text;
            public Vector3 WorldPosition;
            public float Age;
            public Color Color;
        }

        private readonly List<Entry> entries = new List<Entry>();
        private Canvas canvas;

        private void Awake()
        {
            instance = this;
            canvas = GetComponentInParent<Canvas>();
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        public static void Show(Vector3 worldPosition, int amount, bool critical, bool hurtsLocalPlayer)
        {
            if (instance != null) instance.Spawn(worldPosition, amount, critical, hurtsLocalPlayer);
        }

        private void Spawn(Vector3 worldPosition, int amount, bool critical, bool hurtsLocalPlayer)
        {
            Entry entry = entries.Find(e => !e.Text.gameObject.activeSelf) ?? CreateEntry();
            entry.WorldPosition = worldPosition + (Vector3)(Random.insideUnitCircle * 0.1f);
            entry.Age = 0f;
            entry.Color = hurtsLocalPlayer ? new Color(1f, 0.3f, 0.25f) : critical ? new Color(1f, 0.85f, 0.2f) : Color.white;
            entry.Text.text = critical ? amount + "!" : amount.ToString();
            entry.Text.fontSize = critical ? Mathf.RoundToInt(fontSize * 1.4f) : fontSize;
            entry.Text.gameObject.SetActive(true);
            Place(entry);
        }

        private Entry CreateEntry()
        {
            var go = new GameObject("Damage Number", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.fontStyle = FontStyle.Bold;
            go.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            var entry = new Entry { Text = text };
            entries.Add(entry);
            return entry;
        }

        private void LateUpdate()
        {
            foreach (Entry entry in entries)
            {
                if (!entry.Text.gameObject.activeSelf) continue;

                entry.Age += Time.deltaTime;
                if (entry.Age >= lifetime)
                {
                    entry.Text.gameObject.SetActive(false);
                    continue;
                }
                Place(entry);
            }
        }

        private void Place(Entry entry)
        {
            Camera cam = Camera.main;
            if (cam == null) return;

            float t = entry.Age / lifetime;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            Vector3 screen = cam.WorldToScreenPoint(entry.WorldPosition);
            entry.Text.rectTransform.position = screen + Vector3.up * (rise * scale * t);

            Color color = entry.Color;
            color.a = 1f - t * t;
            entry.Text.color = color;
        }
    }
}
