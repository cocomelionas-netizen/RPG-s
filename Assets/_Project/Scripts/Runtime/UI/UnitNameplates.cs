using System.Collections.Generic;
using RPG.Combat;
using RPG.Monsters;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// Names above monsters (red, elites gold) and players (yourself green, others blue, with the rank under the name),
    /// drawn on the HUD canvas just above their health bars. The selected target's name is brighter and shows its level.
    /// </summary>
    // After the camera has moved this frame; otherwise the plates use last frame's camera and drift while walking.
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(RectTransform))]
    public class UnitNameplates : MonoBehaviour
    {
        [Tooltip("Names of units further than this from the camera's center are hidden.")]
        [SerializeField, Min(1f)] private float visibleDistance = 11f;

        private static readonly Color MonsterColor = new Color(0.9f, 0.25f, 0.2f);
        private static readonly Color TargetColor = new Color(1f, 0.45f, 0.35f);
        private static readonly Color PlayerColor = new Color(0.55f, 0.8f, 1f);
        private static readonly Color EliteColor = new Color(1f, 0.75f, 0.2f);
        private static readonly Color SelfColor = new Color(0.45f, 0.95f, 0.5f);
        private const string RankColorHex = "c9d6e8";

        private readonly Dictionary<Health, Text> plates = new Dictionary<Health, Text>();
        private readonly List<Health> gone = new List<Health>();

        private static string LevelCurveRank(int level) => RPG.Progression.LevelCurve.RankName(level);

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            Health self = UIFactory.LocalPlayer<Health>();

            foreach (Health unit in Health.Spawned)
            {
                if (unit == null) continue;
                bool isMonster = unit.GetComponent<MonsterAI>() != null;
                bool isPlayer = unit.Faction == Faction.Player;
                if (!isMonster && !isPlayer) continue;

                if (!plates.TryGetValue(unit, out Text plate))
                {
                    plate = UIFactory.CreateText(unit.name, transform, 14, TextAnchor.LowerCenter);
                    plate.rectTransform.sizeDelta = new Vector2(240f, 44f);
                    plate.rectTransform.pivot = new Vector2(0.5f, 0f);
                    plates[unit] = plate;
                }

                bool show = cam != null && !unit.IsDead && Vector2.Distance(cam.transform.position, unit.transform.position) <= visibleDistance;
                if (plate.gameObject.activeSelf != show) plate.gameObject.SetActive(show);
                if (!show) continue;

                bool targeted = PlayerTargeting.LocalTarget == unit;
                var monster = isMonster ? unit.GetComponent<MonsterAI>() : null;
                var progress = isPlayer ? unit.GetComponent<RPG.Progression.CharacterProgress>() : null;
                // Players: the name with the rank under it, like "<Corporal>".
                plate.text = targeted && monster != null ? $"{unit.DisplayName} <size=12>Lv {monster.Level}</size>"
                    : progress != null ? $"{unit.DisplayName}\n<size=13><color=#{RankColorHex}><{LevelCurveRank(progress.Level)}></color></size>"
                    : unit.DisplayName;
                plate.color = unit == self ? SelfColor : isPlayer ? PlayerColor
                    : monster != null && monster.IsElite ? EliteColor : targeted ? TargetColor : MonsterColor;
                plate.fontStyle = targeted ? FontStyle.Bold : FontStyle.Normal;

                var presenter = unit.GetComponent<HealthPresenter>();
                Vector3 anchor = presenter != null ? presenter.NameplatePosition : unit.transform.position + Vector3.up;
                plate.rectTransform.position = cam.WorldToScreenPoint(anchor);
            }

            // Units that despawned.
            gone.Clear();
            foreach (KeyValuePair<Health, Text> entry in plates)
                if (entry.Key == null || !entry.Key.IsSpawned) gone.Add(entry.Key);
            foreach (Health unit in gone)
            {
                if (plates[unit] != null) Destroy(plates[unit].gameObject);
                plates.Remove(unit);
            }
        }
    }
}
