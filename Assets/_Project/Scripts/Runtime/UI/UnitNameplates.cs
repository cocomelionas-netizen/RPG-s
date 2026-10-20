using System.Collections.Generic;
using RPG.Combat;
using RPG.Monsters;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// Names above monsters (red) and players (light blue, with the rank under the name), drawn on the HUD canvas
    /// just above their health bars. The selected target's name is bold and shows its level.
    /// </summary>
    // After the camera has moved this frame; otherwise the plates use last frame's camera and drift while walking.
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(RectTransform))]
    public class UnitNameplates : MonoBehaviour
    {
        [Tooltip("Names of units further than this from the camera's center are hidden.")]
        [SerializeField, Min(1f)] private float visibleDistance = 11f;

        // Colours taken from the source game: monsters (bosses too) red, players and the line under their name light blue.
        private static readonly Color MonsterColor = new Color32(0xB6, 0x3E, 0x3D, 0xFF);
        private static readonly Color PlayerColor = new Color32(0x8E, 0xAC, 0xD2, 0xFF);

        /// <summary>A unit's plate and what it shows now, with its components looked up once.</summary>
        private sealed class Plate
        {
            public Text Text;
            public MonsterAI Monster;
            public RPG.Progression.CharacterProgress Progress;
            public HealthPresenter Presenter;
            public bool Shown;
            public bool Targeted;
            public int Level = -1;
            public string Name;
        }

        private readonly Dictionary<Health, Plate> plates = new Dictionary<Health, Plate>();
        // Units that never get a plate (neither monster nor player), so they are not looked at again.
        private readonly HashSet<Health> skipped = new HashSet<Health>();
        // Plates of despawned units, used again for the next ones (monsters come and go as players move).
        private readonly Stack<Text> spare = new Stack<Text>();
        private readonly List<Health> gone = new List<Health>();

        private static string LevelCurveRank(int level) => RPG.Progression.LevelCurve.RankName(level);

        private void LateUpdate()
        {
            Camera cam = Camera.main;

            foreach (Health unit in Health.Spawned)
            {
                if (unit == null || skipped.Contains(unit)) continue;
                if (!plates.TryGetValue(unit, out Plate plate))
                {
                    var monster = unit.GetComponent<MonsterAI>();
                    if (monster == null && unit.Faction != Faction.Player)
                    {
                        skipped.Add(unit);
                        continue;
                    }
                    plate = new Plate
                    {
                        Text = TakeText(unit.name),
                        Monster = monster,
                        Progress = monster == null ? unit.GetComponent<RPG.Progression.CharacterProgress>() : null,
                        Presenter = unit.GetComponent<HealthPresenter>(),
                    };
                    plate.Text.color = monster == null ? PlayerColor : MonsterColor;
                    plates[unit] = plate;
                }

                bool show = cam != null && !unit.IsDead && Vector2.Distance(cam.transform.position, unit.transform.position) <= visibleDistance;
                if (plate.Shown != show)
                {
                    plate.Shown = show;
                    plate.Text.gameObject.SetActive(show);
                }
                if (!show) continue;

                // The text is rewritten only when the name, level or selection changed.
                bool targeted = PlayerTargeting.LocalTarget == unit;
                int level = plate.Monster != null ? plate.Monster.Level : plate.Progress != null ? plate.Progress.Level : 0;
                string unitName = unit.DisplayName;
                if (targeted != plate.Targeted || level != plate.Level || unitName != plate.Name)
                {
                    plate.Targeted = targeted;
                    plate.Level = level;
                    plate.Name = unitName;
                    // Players: the name with the rank under it, like "<Corporal>".
                    plate.Text.text = targeted && plate.Monster != null ? $"{unitName} <size=12>Lv {level}</size>"
                        : plate.Progress != null ? $"{unitName}\n<{LevelCurveRank(level)}>"
                        : unitName;
                    plate.Text.fontStyle = targeted ? FontStyle.Bold : FontStyle.Normal;
                }

                Vector3 anchor = plate.Presenter != null ? plate.Presenter.NameplatePosition : unit.transform.position + Vector3.up;
                plate.Text.rectTransform.position = cam.WorldToScreenPoint(anchor);
            }

            // Units that despawned: their plates are kept for the next ones.
            gone.Clear();
            foreach (KeyValuePair<Health, Plate> entry in plates)
                if (entry.Key == null || !entry.Key.IsSpawned) gone.Add(entry.Key);
            foreach (Health unit in gone)
            {
                Text text = plates[unit].Text;
                plates.Remove(unit);
                if (text == null) continue;
                text.gameObject.SetActive(false);
                spare.Push(text);
            }
            if (skipped.Count > 0) skipped.RemoveWhere(unit => unit == null || !unit.IsSpawned);
        }

        private Text TakeText(string unitName)
        {
            Text text = spare.Count > 0 ? spare.Pop() : null;
            if (text == null)
            {
                text = UIFactory.CreateText(unitName, transform, 14, TextAnchor.LowerCenter);
                text.rectTransform.sizeDelta = new Vector2(240f, 44f);
                text.rectTransform.pivot = new Vector2(0.5f, 0f);
            }
            text.gameObject.SetActive(false);
            return text;
        }
    }
}
