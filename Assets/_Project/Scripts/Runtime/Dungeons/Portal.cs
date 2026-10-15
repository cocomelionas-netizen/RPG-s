using System.Collections.Generic;
using UnityEngine;

namespace RPG.Dungeons
{
    public enum PortalKind
    {
        /// <summary>In the world: takes the group standing around it into a new dungeon instance.</summary>
        DungeonEntrance,
        /// <summary>In a dungeon: takes the player back to the entrance.</summary>
        DungeonExit,
    }

    /// <summary>
    /// A portal the player clicks to travel. The click and the walk are handled by <see cref="RPG.Items.LootPicker"/>,
    /// the travel itself by the server (<see cref="DungeonManager.ServerUsePortal"/>), which checks the distance.
    /// Exits that need the boss dead look faded until then.
    /// </summary>
    public class Portal : MonoBehaviour
    {
        [SerializeField] private PortalKind kind = PortalKind.DungeonEntrance;
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private bool requiresBossDefeated;

        private static readonly List<Portal> All = new List<Portal>();

        public PortalKind Kind => kind;

        /// <summary>The dungeon an exit belongs to (null for entrances).</summary>
        public DungeonInstance Instance { get; private set; }

        public bool IsOpen => !requiresBossDefeated || (Instance != null && Instance.BossDefeated);

        public static IReadOnlyList<Portal> Portals => All;

        /// <summary>Sets up a portal created at runtime (dungeon exits) or by the scene builder.</summary>
        public void Configure(PortalKind portalKind, SpriteRenderer visual, DungeonInstance owner, bool needsBossDefeated)
        {
            kind = portalKind;
            spriteRenderer = visual;
            Instance = owner;
            requiresBossDefeated = needsBossDefeated;
        }

        private void OnEnable() => All.Add(this);

        private void OnDisable() => All.Remove(this);

        private void Update()
        {
            if (spriteRenderer == null) return;
            Color color = spriteRenderer.color;
            color.a = IsOpen ? 1f : 0.3f;
            spriteRenderer.color = color;
            // A slow shimmer so portals look alive.
            float pulse = 1f + Mathf.Sin(Time.time * 2.5f) * 0.04f;
            spriteRenderer.transform.localScale = new Vector3(pulse, pulse, 1f);
        }
    }
}
