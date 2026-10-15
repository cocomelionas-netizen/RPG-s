using System.Collections.Generic;
using RPG.Vendors;
using RPG.World;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>Names (and what they sell) above the vendor NPCs, and the names of chests, drawn on the HUD canvas.</summary>
    // After the camera has moved this frame; otherwise the plates use last frame's camera and drift while walking.
    [DefaultExecutionOrder(1000)]
    [RequireComponent(typeof(RectTransform))]
    public class NpcNameplates : MonoBehaviour
    {
        [SerializeField] private float heightAboveFeet = 0.85f;
        [Tooltip("Plates of NPCs further than this from the camera's center are hidden.")]
        [SerializeField, Min(1f)] private float visibleDistance = 12f;

        private static readonly Color NameColor = new Color(1f, 0.85f, 0.45f);
        private readonly Dictionary<Vendor, Text> plates = new Dictionary<Vendor, Text>();
        private readonly Dictionary<LootChest, Text> chestPlates = new Dictionary<LootChest, Text>();
        private readonly List<LootChest> goneChests = new List<LootChest>();
        // Off-white, as Source writes container names.
        private static readonly Color ChestColor = new Color32(0xE8, 0xED, 0xE1, 0xFF);
        private const float ChestNameHeight = 0.55f;
        private Canvas canvas;

        private void Awake()
        {
            canvas = GetComponentInParent<Canvas>();
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            foreach (Vendor vendor in Vendor.All)
            {
                if (!plates.TryGetValue(vendor, out Text plate))
                {
                    plate = UIFactory.CreateText(vendor.name, transform, 16, TextAnchor.LowerCenter);
                    plate.rectTransform.sizeDelta = new Vector2(240f, 44f);
                    plate.rectTransform.pivot = new Vector2(0.5f, 0f);
                    plate.color = NameColor;
                    string title = vendor.Definition != null ? vendor.Definition.title : "";
                    plate.text = string.IsNullOrEmpty(title) ? vendor.DisplayName : $"{vendor.DisplayName}\n<size=13>{title}</size>";
                    plates[vendor] = plate;
                }

                bool show = cam != null && Vector2.Distance(cam.transform.position, vendor.transform.position) <= visibleDistance;
                plate.gameObject.SetActive(show);
                if (show)
                    plate.rectTransform.position = cam.WorldToScreenPoint(vendor.transform.position + Vector3.up * heightAboveFeet);
            }

            foreach (LootChest chest in LootChest.All)
            {
                if (!chestPlates.TryGetValue(chest, out Text plate))
                {
                    plate = UIFactory.CreateText(chest.name, transform, 14, TextAnchor.LowerCenter);
                    plate.rectTransform.sizeDelta = new Vector2(220f, 40f);
                    plate.rectTransform.pivot = new Vector2(0.5f, 0f);
                    plate.color = ChestColor;
                    plate.text = chest.Locked
                        ? $"{chest.Label}\n<size=12><color=#b5a07a>Locked (Lockpicking {chest.RequiredLockpicking})</color></size>"
                        : chest.Label;
                    chestPlates[chest] = plate;
                }

                bool show = cam != null && Vector2.Distance(cam.transform.position, chest.transform.position) <= visibleDistance;
                if (plate.gameObject.activeSelf != show) plate.gameObject.SetActive(show);
                if (show)
                    plate.rectTransform.position = cam.WorldToScreenPoint(chest.transform.position + Vector3.up * ChestNameHeight);
            }

            // Chests that were opened.
            goneChests.Clear();
            foreach (KeyValuePair<LootChest, Text> entry in chestPlates)
                if (entry.Key == null || !entry.Key.IsSpawned) goneChests.Add(entry.Key);
            foreach (LootChest chest in goneChests)
            {
                if (chestPlates[chest] != null) Destroy(chestPlates[chest].gameObject);
                chestPlates.Remove(chest);
            }
        }
    }
}
