using RPG.World;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// "Entering Brute Hills": a large title that fades in and out when the local player walks into another zone
    /// (also written to the message log). Builds its own UI; place it on a RectTransform in the top middle of the HUD.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class ZoneBanner : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float showSeconds = 3f;
        [SerializeField, Min(0.1f)] private float checkInterval = 0.3f;

        private static readonly Color TitleColor = new Color(1f, 0.88f, 0.6f);

        private Text title;
        private string currentZone;
        private float shownAt = -100f;
        private float nextCheck;

        private void Awake()
        {
            title = UIFactory.CreateText("Title", transform, 40, TextAnchor.MiddleCenter);
            UIFactory.Stretch(title.rectTransform);
            title.fontStyle = FontStyle.Bold;
            title.horizontalOverflow = HorizontalWrapMode.Overflow;
            title.color = Color.clear;
        }

        private void LateUpdate()
        {
            if (Time.time >= nextCheck)
            {
                nextCheck = Time.time + checkInterval;
                Transform player = UIFactory.LocalPlayer<Transform>();
                string zone = player != null ? Zone.NameAt(player.position) : null;
                if (player == null) currentZone = null;
                else if (zone != currentZone)
                {
                    currentZone = zone;
                    if (!string.IsNullOrEmpty(zone))
                    {
                        title.text = zone;
                        shownAt = Time.time;
                        MessageLog.Add($"Entering {zone}.", TitleColor);
                    }
                }
            }

            // Fade in for a quarter second, hold, fade out for the last second.
            float age = Time.time - shownAt;
            float alpha = age < 0.25f ? age / 0.25f : Mathf.Clamp01(showSeconds - age);
            Color color = TitleColor;
            color.a = alpha;
            title.color = color;
        }
    }
}
