using System.Collections.Generic;
using RPG.Navigation;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// A named area of a map (a town, a valley). Only for show: the minimap and the "Entering ..." banner use it.
    /// Where zones overlap the smallest wins; outside every zone the map's own name is used.
    /// </summary>
    public class Zone : MonoBehaviour
    {
        [SerializeField] private string displayName = "Zone";
        [SerializeField, Min(0.5f)] private float radius = 5f;

        private static readonly List<Zone> Zones = new List<Zone>();

        public string DisplayName => displayName;
        public float Radius => radius;

        public void Configure(string zoneName, float zoneRadius)
        {
            displayName = zoneName;
            radius = zoneRadius;
        }

        private void OnEnable() => Zones.Add(this);

        private void OnDisable() => Zones.Remove(this);

        /// <summary>The name of the place at a world position ("" when on no map).</summary>
        public static string NameAt(Vector2 position)
        {
            Zone best = null;
            foreach (Zone zone in Zones)
            {
                if (Vector2.Distance(position, zone.transform.position) > zone.radius) continue;
                if (best == null || zone.radius < best.radius) best = zone;
            }
            if (best != null) return best.displayName;

            WorldNavigation map = WorldNavigation.At(position);
            return map != null ? map.ZoneName : "";
        }
    }
}
