using System.Collections.Generic;
using RPG.Dungeons;
using RPG.Vendors;
using RPG.World;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>
    /// Names (and what they sell) above the vendor NPCs, the names (and titles) of townsfolk, the names of chests, and of
    /// dungeon portals (with who may enter). Each plate is a small world-space canvas standing over its NPC or object, so
    /// nothing is redrawn while the camera moves (screen-space plates had to be moved and their canvas rebuilt every frame
    /// of walking, which grew slow with the townsfolk). The plates are as big as HUD text and follow the camera's zoom;
    /// far ones are hidden a few times a second. Place it anywhere (it keeps its own lists).
    /// </summary>
    public class NpcNameplates : MonoBehaviour
    {
        [SerializeField] private float heightAboveFeet = 0.85f;
        [Tooltip("Plates of NPCs further than this from the camera's center are hidden.")]
        [SerializeField, Min(1f)] private float visibleDistance = 12f;

        private static readonly Color NameColor = new Color(1f, 0.85f, 0.45f);
        private static readonly Color PersonColor = new Color(0.65f, 0.95f, 0.55f);
        private static readonly Color PortalColor = new Color(0.85f, 0.7f, 1f);
        // Off-white, as the source game writes container names.
        private static readonly Color ChestColor = new Color32(0xE8, 0xED, 0xE1, 0xFF);
        private const float PortalNameHeight = 1f;
        private const float ChestNameHeight = 0.55f;
        /// <summary>The HUD's reference height: a plate's text is as big as HUD text of the same size.</summary>
        private const float ReferenceHeight = 1080f;
        private const float CheckInterval = 0.25f;
        /// <summary>World-space canvases draw over the world's sprites.</summary>
        private const int PlateSortingOrder = 1000;

        private readonly Dictionary<Object, GameObject> plates = new Dictionary<Object, GameObject>();
        private readonly List<Object> gone = new List<Object>();
        private float nextCheck;
        private float scale;

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            float wanted = 2f * cam.orthographicSize / ReferenceHeight;
            bool rescale = Mathf.Abs(wanted - scale) > scale * 0.01f;
            if (rescale) scale = wanted;
            if (!rescale && Time.unscaledTime < nextCheck) return;
            nextCheck = Time.unscaledTime + CheckInterval;
            Vector2 center = cam.transform.position;

            foreach (Vendor vendor in Vendor.All)
            {
                string title = vendor.Definition != null ? vendor.Definition.title : "";
                Show(vendor, vendor.transform, center, heightAboveFeet, 16, NameColor,
                    () => string.IsNullOrEmpty(title) ? vendor.DisplayName : $"{vendor.DisplayName}\n<size=13>{title}</size>", rescale);
            }
            foreach (Townsperson person in Townsperson.All)
                Show(person, person.transform, center, heightAboveFeet, 15, PersonColor,
                    () => string.IsNullOrEmpty(person.Title) ? person.DisplayName : $"{person.DisplayName}\n<size=12>{person.Title}</size>", rescale);
            foreach (LootChest chest in LootChest.All)
            {
                if (!chest.IsSpawned) continue;
                Show(chest, chest.transform, center, ChestNameHeight, 14, ChestColor,
                    () => chest.Locked
                        ? $"{chest.Label}\n<size=12><color=#b5a07a>Locked (Lockpicking {chest.RequiredLockpicking})</color></size>"
                        : chest.Label, rescale);
            }
            foreach (Portal portal in Portal.Portals)
                if (portal != null && portal.isActiveAndEnabled)
                    Show(portal, portal.transform, center, PortalNameHeight, 14, PortalColor, () => PortalText(portal), rescale);

            // Opened chests and closed portals lose their plates.
            gone.Clear();
            foreach (KeyValuePair<Object, GameObject> entry in plates)
            {
                bool alive = entry.Key != null && entry.Value != null
                             && (!(entry.Key is LootChest chest) || chest.IsSpawned)
                             && (!(entry.Key is Portal portal) || portal.isActiveAndEnabled);
                if (!alive) gone.Add(entry.Key);
            }
            foreach (Object owner in gone)
            {
                if (plates[owner] != null) Destroy(plates[owner]);
                plates.Remove(owner);
            }
        }

        /// <summary>Makes the plate of <paramref name="owner"/> once, then only shows or hides it by distance (and rescales on zoom).</summary>
        private void Show(Object owner, Transform target, Vector2 center, float height, int fontSize, Color color, System.Func<string> text,
            bool rescale)
        {
            bool near = Vector2.Distance(center, target.position) <= visibleDistance;
            if (!plates.TryGetValue(owner, out GameObject plate) || plate == null)
            {
                if (!near) return;
                plate = CreatePlate(target, height, fontSize, color, text());
                plates[owner] = plate;
            }
            if (plate.activeSelf != near) plate.SetActive(near);
            if (rescale || plate.transform.localScale.x != scale) plate.transform.localScale = Vector3.one * scale;
        }

        private GameObject CreatePlate(Transform target, float height, int fontSize, Color color, string content)
        {
            var root = new GameObject("Nameplate", typeof(RectTransform));
            var rect = (RectTransform)root.transform;
            rect.SetParent(target, false);
            rect.localPosition = Vector3.up * height;
            rect.localScale = Vector3.one * scale;
            rect.sizeDelta = new Vector2(260f, 48f);
            rect.pivot = new Vector2(0.5f, 0f);
            var plateCanvas = root.AddComponent<Canvas>();
            plateCanvas.renderMode = RenderMode.WorldSpace;
            plateCanvas.sortingOrder = PlateSortingOrder;

            Text label = UIFactory.CreateText("Name", rect, fontSize, TextAnchor.LowerCenter);
            UIFactory.Stretch(label.rectTransform);
            label.color = color;
            label.text = content;
            return root;
        }

        /// <summary>"Crumbling Tower" over "Level 25 · Tower Key · up to 4 players"; "Exit" or "Exit to St. Aurelis".</summary>
        private static string PortalText(Portal portal)
        {
            if (portal.Kind == PortalKind.DungeonExit)
            {
                DungeonDefinition inside = portal.Instance != null ? portal.Instance.Definition : null;
                bool elsewhere = !string.IsNullOrEmpty(portal.Label) && portal.Label != "Leave" && (inside == null || portal.Label != inside.displayName);
                return elsewhere ? $"Exit to {portal.Label}" : "Exit";
            }

            DungeonDefinition dungeon = portal.Dungeon != null ? portal.Dungeon : DungeonManager.Instance != null ? DungeonManager.Instance.Definition : null;
            if (dungeon == null) return portal.Label ?? "";
            var rules = new List<string>();
            if (dungeon.minLevel > 1) rules.Add($"Level {dungeon.minLevel}");
            if (dungeon.key != null) rules.Add(dungeon.key.displayName);
            rules.Add(dungeon.maxPlayers == 1 ? "alone" : $"up to {dungeon.maxPlayers} players");
            return $"{dungeon.displayName}\n<size=12><color=#b5a07a>{string.Join(" · ", rules)}</color></size>";
        }
    }
}
