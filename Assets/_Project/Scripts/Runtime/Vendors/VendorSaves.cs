using System;
using System.IO;
using UnityEngine;

namespace RPG.Vendors
{
    /// <summary>
    /// Where vendors keep their stock between sessions (the server's): what is left of each limited item, the random
    /// qualities in stock and when each item restocks, in real time. In a browser in PlayerPrefs (its own storage), else
    /// as JSON files beside the accounts. Off unless the game turns it on (tests leave it off).
    /// </summary>
    public static class VendorSaves
    {
        private const string Prefix = "rpg.vendor.";

        [Serializable]
        public class State
        {
            public int[] remaining;
            public int[] affixes;
            /// <summary>Seconds since 1970 (UTC) when each stock item restocks.</summary>
            public double[] restockAt;
        }

        /// <summary>Whether vendors save and load (set by <see cref="RPG.Networking.NetworkBootstrap"/> in the game).</summary>
        public static bool Enabled { get; set; }

        /// <summary>Now, in seconds since 1970 (UTC).</summary>
        public static double Now => (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;

        private static bool InBrowser => Application.platform == RuntimePlatform.WebGLPlayer;

        private static string PathFor(string key) => Path.Combine(Application.persistentDataPath, "Vendors", key + ".json");

        public static void Save(string key, State state)
        {
            string json = JsonUtility.ToJson(state);
            try
            {
                if (InBrowser)
                {
                    PlayerPrefs.SetString(Prefix + key, json);
                    PlayerPrefs.Save();
                    return;
                }
                string path = PathFor(key);
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Vendors] Could not save {key}: {exception.Message}");
            }
        }

        public static bool TryLoad(string key, out State state)
        {
            state = null;
            try
            {
                string json = InBrowser ? PlayerPrefs.GetString(Prefix + key, "")
                    : File.Exists(PathFor(key)) ? File.ReadAllText(PathFor(key)) : "";
                if (string.IsNullOrEmpty(json)) return false;
                state = JsonUtility.FromJson<State>(json);
                return state != null;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Vendors] Could not read {key}: {exception.Message}");
                return false;
            }
        }

        /// <summary>Server: every vendor writes its stock now (the server stops, the game closes).</summary>
        public static void SaveAll()
        {
            foreach (Vendor vendor in Vendor.All)
                if (vendor != null) vendor.Save();
        }
    }
}
