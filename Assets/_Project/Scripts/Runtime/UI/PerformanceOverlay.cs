using RPG.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// F3 shows how the game runs: frames per second, the slowest frame of the last second, how many units and monsters
    /// are spawned, and the managed memory in use. For checking a change against a target ("steady 60 FPS").
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
            text = UIFactory.CreateText("Performance", transform, 14, TextAnchor.UpperLeft);
            UIFactory.Stretch(text.rectTransform);
            text.color = new Color(0.75f, 1f, 0.75f);
            text.gameObject.SetActive(false);
        }

        private void Update()
        {
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.f3Key.wasPressedThisFrame) text.gameObject.SetActive(!text.gameObject.activeSelf);

            float delta = Time.unscaledDeltaTime;
            frames++;
            elapsed += delta;
            worst = Mathf.Max(worst, delta);
            if (elapsed < 1f) return;

            if (text.gameObject.activeSelf)
            {
                int units = 0;
                int monsters = 0;
                foreach (Health unit in Health.Spawned)
                {
                    if (unit == null) continue;
                    units++;
                    if (unit.Faction == Faction.Monster) monsters++;
                }
                text.text = $"FPS {frames / elapsed:0}   slowest frame {worst * 1000f:0} ms\n" +
                            $"units {units}   monsters {monsters}   memory {System.GC.GetTotalMemory(false) / (1024 * 1024)} MB";
            }
            frames = 0;
            elapsed = 0f;
            worst = 0f;
        }
    }
}
