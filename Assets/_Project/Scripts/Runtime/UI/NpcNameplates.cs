using System.Collections.Generic;
using RPG.Vendors;
using UnityEngine;
using UnityEngine.UI;

namespace RPG.UI
{
    /// <summary>Names (and what they sell) above the vendor NPCs, drawn on the HUD canvas.</summary>
    [RequireComponent(typeof(RectTransform))]
    public class NpcNameplates : MonoBehaviour
    {
        [SerializeField] private float heightAboveFeet = 1.05f;
        [Tooltip("Plates of NPCs further than this from the camera's center are hidden.")]
        [SerializeField, Min(1f)] private float visibleDistance = 12f;

        private static readonly Color NameColor = new Color(1f, 0.85f, 0.45f);
        private readonly Dictionary<Vendor, Text> plates = new Dictionary<Vendor, Text>();
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
        }
    }
}
