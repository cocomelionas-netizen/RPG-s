using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace RPG.World
{
    /// <summary>
    /// Day and night, seen the same by everyone (the time of day follows the real clock, <see cref="CycleMinutes"/> a day):
    /// the global light dims and turns blue at night, as dark as the zone the local player stands in gets (the source game's
    /// zone_template night_pct, <see cref="Configure"/>); dungeons are that dark all the time. The map's lights (campfires,
    /// street lights, torches, fireflies) shine in the dark, and a soft light around the local player keeps the way visible.
    /// Only for show: it runs on every peer for its own player and changes nothing on the server.
    /// </summary>
    public class DayNight : MonoBehaviour
    {
        /// <summary>Real minutes of one day and night.</summary>
        public const float CycleMinutes = 40f;

        // Parts of the cycle: day, dusk, night, dawn (the rest of the cycle).
        private const float DayPart = 0.6f, DuskPart = 0.075f, NightPart = 0.25f;
        // How much a zone's night_pct adds to the night's own darkness outdoors, and the least light left.
        private const float NightDarkness = 0.4f, UndergroundDarkness = 0.25f, MinimumLight = 0.22f;
        private static readonly Color NightTint = new Color(0.55f, 0.65f, 1f);
        private static readonly Color DuskTint = new Color(1f, 0.78f, 0.6f);

        [SerializeField] private Light2D globalLight;
        [SerializeField] private Light2D playerLight;
        [SerializeField] private Light2D[] mapLights = Array.Empty<Light2D>();
        [SerializeField] private string[] zones = Array.Empty<string>();
        [SerializeField] private float[] nights = Array.Empty<float>();
        [Tooltip("Zones that are underground (dungeons): as dark as their night all day.")]
        [SerializeField] private string[] underground = Array.Empty<string>();

        private float[] mapLightIntensity = Array.Empty<float>();
        private float shownDarkness = -1f;
        private bool lightsOn = true;
        private string lastZone;
        private float zoneNight;
        private bool zoneUnderground;

        /// <summary>How dark it is outdoors now, 0 (day) to 1 (deepest night): for tests and the world map.</summary>
        public static float NightAmount { get; private set; }

        /// <summary>Seconds added to the clock on this screen only (the cheat window's Day / night button).</summary>
        public static double ClockShift { get; set; }

        /// <summary>Cheats: jumps this screen to the middle of the night, or (at night) back to the clock's own time.</summary>
        public static void ToggleNight()
        {
            if (ClockShift != 0.0)
            {
                ClockShift = 0.0;
                return;
            }
            double cycle = CycleMinutes * 60.0;
            double now = Now();
            double midnight = (DayPart + DuskPart + NightPart / 2f) * cycle;
            ClockShift = ((midnight - now % cycle) % cycle + cycle) % cycle;
        }

        private static double Now() => DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds;

        public void Configure(Light2D global, Light2D aroundPlayer, Light2D[] lights, (string zone, float night)[] zoneNights,
            string[] undergroundZones)
        {
            globalLight = global;
            playerLight = aroundPlayer;
            mapLights = lights ?? Array.Empty<Light2D>();
            zones = new string[zoneNights.Length];
            nights = new float[zoneNights.Length];
            for (int i = 0; i < zoneNights.Length; i++)
            {
                zones[i] = zoneNights[i].zone;
                nights[i] = zoneNights[i].night;
            }
            underground = undergroundZones ?? Array.Empty<string>();
        }

        private void Awake()
        {
            mapLightIntensity = new float[mapLights.Length];
            for (int i = 0; i < mapLights.Length; i++)
                mapLightIntensity[i] = mapLights[i] != null ? mapLights[i].intensity : 0f;
        }

        /// <summary>
        /// The night at a point of the cycle (seconds since any day began): 0 all day, rising through dusk to 1 at night,
        /// falling through dawn.
        /// </summary>
        public static float NightAt(double seconds)
        {
            double cycle = CycleMinutes * 60.0;
            float phase = (float)(((seconds % cycle) + cycle) % cycle / cycle);
            if (phase < DayPart) return 0f;
            if (phase < DayPart + DuskPart) return Mathf.SmoothStep(0f, 1f, (phase - DayPart) / DuskPart);
            if (phase < DayPart + DuskPart + NightPart) return 1f;
            return Mathf.SmoothStep(1f, 0f, (phase - DayPart - DuskPart - NightPart) / (1f - DayPart - DuskPart - NightPart));
        }

        /// <summary>How dark a zone is: at <paramref name="night"/> (0-1) with the zone's night_pct, or underground.</summary>
        public static float DarknessOf(float night, float zoneNight, bool isUnderground) =>
            Mathf.Min(1f - MinimumLight, isUnderground ? UndergroundDarkness + zoneNight : night * (NightDarkness + zoneNight));

        private void Update()
        {
            float night = NightAt(Now() + ClockShift);
            NightAmount = night;
            Transform player = LocalPlayer();
            Vector2 position = player != null ? (Vector2)player.position
                : Camera.main != null ? (Vector2)Camera.main.transform.position : Vector2.zero;
            UpdateZone(position);

            float darkness = DarknessOf(night, zoneNight, zoneUnderground);
            // Walking from one zone into another the light changes over a couple of seconds.
            shownDarkness = shownDarkness < 0f ? darkness : Mathf.MoveTowards(shownDarkness, darkness, Time.deltaTime * 0.25f);
            Apply(shownDarkness, night, player);
        }

        private void UpdateZone(Vector2 position)
        {
            string zone = Zone.NameAt(position) ?? "";
            if (zone == lastZone) return;
            lastZone = zone;
            zoneNight = NightOf(zone);
            zoneUnderground = Array.IndexOf(underground, zone) >= 0;
        }

        private float NightOf(string zone)
        {
            int index = Array.IndexOf(zones, zone);
            if (index < 0) index = Array.IndexOf(zones, "");
            return index >= 0 ? nights[index] : 0.2f;
        }

        private void Apply(float darkness, float night, Transform player)
        {
            if (globalLight != null)
            {
                globalLight.intensity = 1f - darkness;
                // Blue at night, warm at dusk and dawn; underground a cold blue whatever the hour.
                float dusk = zoneUnderground ? 0f : 4f * night * (1f - night);
                Color tint = Color.Lerp(Color.white, NightTint, Mathf.Clamp01(darkness / (1f - MinimumLight)));
                globalLight.color = Color.Lerp(tint, DuskTint, dusk * 0.6f);
            }

            bool on = darkness > 0.05f;
            float strength = Mathf.Clamp01(darkness / 0.5f);
            if (playerLight != null)
            {
                playerLight.enabled = on && player != null;
                if (player != null) playerLight.transform.position = player.position + Vector3.up * 0.3f;
                playerLight.intensity = 0.55f * strength;
            }
            if (on != lightsOn)
            {
                lightsOn = on;
                foreach (Light2D light in mapLights)
                    if (light != null) light.enabled = on;
            }
            if (!on) return;
            for (int i = 0; i < mapLights.Length; i++)
                if (mapLights[i] != null) mapLights[i].intensity = mapLightIntensity[i] * strength;
        }

        private static Transform LocalPlayer()
        {
            NetworkManager network = NetworkManager.Singleton;
            if (network == null || !network.IsListening || network.LocalClient == null) return null;
            NetworkObject playerObject = network.LocalClient.PlayerObject;
            return playerObject != null ? playerObject.transform : null;
        }
    }
}
