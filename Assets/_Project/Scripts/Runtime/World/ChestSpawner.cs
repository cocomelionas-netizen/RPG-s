using Unity.Netcode;
using UnityEngine;

namespace RPG.World
{
    /// <summary>
    /// Server: keeps a chest at this spot. Spawns it when the server starts and again some time after it was opened.
    /// Does nothing on clients. The chest prefab must be a registered network prefab.
    /// </summary>
    public class ChestSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject chestPrefab;
        [SerializeField, Min(0f)] private float respawnSeconds = 180f;
        [Tooltip("The level of the chest at this spot (0 keeps the prefab's).")]
        [SerializeField, Min(0)] private int level;

        private NetworkObject current;
        private float respawnAt = -1f;
        private bool running;

        /// <summary>Server only: the chest standing here now (null while it is waiting to come back).</summary>
        public NetworkObject Current => current != null && current.IsSpawned ? current : null;

        public void Configure(GameObject prefab, float respawnDelay, int chestLevel = 0)
        {
            chestPrefab = prefab;
            respawnSeconds = Mathf.Max(0f, respawnDelay);
            level = Mathf.Max(0, chestLevel);
        }

        private void Update()
        {
            NetworkManager network = NetworkManager.Singleton;
            bool isServer = network != null && network.IsListening && network.IsServer;
            if (!isServer)
            {
                // The network shutdown already destroyed the spawned chest.
                running = false;
                current = null;
                respawnAt = -1f;
                return;
            }

            if (!running)
            {
                running = true;
                Spawn();
                return;
            }

            if (current != null && current.IsSpawned) return;
            if (respawnAt < 0f)
            {
                current = null;
                respawnAt = Time.time + respawnSeconds;
            }
            else if (Time.time >= respawnAt)
            {
                Spawn();
            }
        }

        private void Spawn()
        {
            respawnAt = -1f;
            if (chestPrefab == null) return;
            GameObject chest = Instantiate(chestPrefab, transform.position, Quaternion.identity);
            if (level > 0 && chest.TryGetComponent(out LootChest lootChest)) lootChest.ServerOverrideLevel(level);
            current = chest.GetComponent<NetworkObject>();
            current.Spawn(destroyWithScene: true);
        }
    }
}
