using RPG.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// F8 shows how the game runs: frames per second, the slowest frame of the last second, how many units and monsters
    /// are spawned, and the managed memory in use. For checking a change against a target ("steady 60 FPS"). (F8, not
    /// F3: browsers use F3 for searching the page.) It has its own canvas, so rewriting it does not rebuild the HUD.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class PerformanceOverlay : MonoBehaviour
    {
        private Text text;
        private int frames;
        private float elapsed;
        private float worst;

        private void Awake()
        {
            gameObject.AddComponent<Canvas>();
            text = UIFactory.CreateText("Performance", transform, 14, TextAnchor.UpperLeft);
            UIFactory.Stretch(text.rectTransform);
            text.color = new Color(0.75f, 1f, 0.75f);
            text.gameObject.SetActive(false);
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f8Key.wasPressedThisFrame)
            {
                bool show = !text.gameObject.activeSelf;
                text.gameObject.SetActive(show);
                // Opened: show the numbers now (what was measured so far), not last time's until the next second.
                if (show && frames > 0) Show();
            }

            float delta = Time.unscaledDeltaTime;
            frames++;
            elapsed += delta;
            worst = Mathf.Max(worst, delta);
            if (elapsed < 1f) return;

            if (text.gameObject.activeSelf) Show();
            frames = 0;
            elapsed = 0f;
            worst = 0f;
        }

        private void Show()
        {
            int units = 0;
            int monsters = 0;
            foreach (Health unit in Health.Spawned)
            {
                if (unit == null) continue;
                units++;
                if (unit.Faction == Faction.Monster) monsters++;
            }
            text.text = $"FPS {frames / Mathf.Max(elapsed, 0.001f):0}   slowest frame {worst * 1000f:0} ms\n" +
                        $"units {units}   monsters {monsters}   memory {System.GC.GetTotalMemory(false) / (1024 * 1024)} MB";
        }
    }
}
