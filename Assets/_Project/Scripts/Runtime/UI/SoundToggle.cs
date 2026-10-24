using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>The HUD's sound button, left of the Spells button: turns all sound off and on (remembered on this computer).</summary>
    public class SoundToggle : MonoBehaviour
    {
        private Text label;

        private void Awake()
        {
            Button toggle = UIFactory.CreateButton("Sound Button", transform, new Color(0.22f, 0.25f, 0.3f, 0.95f), "Sound: on", 16);
            var rect = (RectTransform)toggle.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
            rect.sizeDelta = new Vector2(110f, 44f);
            rect.anchoredPosition = new Vector2(-410f, 20f);
            label = toggle.GetComponentInChildren<Text>();
            toggle.onClick.AddListener(() =>
            {
                RPG.World.GameAudio.Muted = !RPG.World.GameAudio.Muted;
                Show();
            });
            Show();
        }

        private void Show()
        {
            if (label != null) label.text = RPG.World.GameAudio.Muted ? "Sound: off" : "Sound: on";
        }
    }
}
