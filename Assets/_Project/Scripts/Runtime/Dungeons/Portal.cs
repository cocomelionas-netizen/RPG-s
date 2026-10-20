using System.Collections.Generic;
using UnityEngine;

namespace RPG.Dungeons
{
    public enum PortalKind
    {
        /// <summary>Takes the group standing around it into a new instance of its dungeon (in the world or in a dungeon).</summary>
        DungeonEntrance,
        /// <summary>In a dungeon: takes the player back out into the world.</summary>
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
        [Tooltip("Entrances: the dungeon it opens (empty: the manager's first).")]
        [SerializeField] private DungeonDefinition dungeon;
        [Tooltip("Exits: where in the world it leads.")]
        [SerializeField] private Vector2 destination;
        [SerializeField] private bool hasDestination;

        private static readonly List<Portal> All = new List<Portal>();

        public PortalKind Kind => kind;

        /// <summary>Entrances: the dungeon it opens (null: the manager's first).</summary>
        public DungeonDefinition Dungeon => dungeon;

        /// <summary>The dungeon instance the portal stands in (null in the world).</summary>
        public DungeonInstance Instance { get; private set; }

        public bool HasDestination => hasDestination;
        public Vector2 Destination => destination;

        public bool IsOpen => !requiresBossDefeated || (Instance != null && Instance.BossDefeated);

        /// <summary>What it is called on hover and in messages: the dungeon it opens, or where an exit leads.</summary>
        public string Label { get; private set; }

        public static ListView<Portal> Portals => new ListView<Portal>(All);

        /// <summary>Sets up a portal created at runtime (dungeon exits) or by the scene builder.</summary>
        public void Configure(PortalKind portalKind, SpriteRenderer visual, DungeonInstance owner, bool needsBossDefeated)
        {
            kind = portalKind;
            spriteRenderer = visual;
            Instance = owner;
            requiresBossDefeated = needsBossDefeated;
            if (string.IsNullOrEmpty(Label) && dungeon != null) Label = dungeon.displayName;
        }

        /// <summary>An entrance into <paramref name="target"/>.</summary>
        public void ConfigureEntrance(SpriteRenderer visual, DungeonInstance owner, DungeonDefinition target)
        {
            dungeon = target;
            Label = target != null ? target.displayName : null;
            Configure(PortalKind.DungeonEntrance, visual, owner, false);
        }

        /// <summary>An exit of a dungeon instance to a point in the world.</summary>
        public void ConfigureExit(SpriteRenderer visual, DungeonInstance owner, Vector2 worldDestination, string label)
        {
            destination = worldDestination;
            hasDestination = true;
            Label = label;
            Configure(PortalKind.DungeonExit, visual, owner, false);
        }

        private void Awake()
        {
            if (string.IsNullOrEmpty(Label) && dungeon != null) Label = dungeon.displayName;
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
