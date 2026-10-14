using System.Collections.Generic;
using RPG.Navigation;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Monsters
{
    /// <summary>
    /// Server: keeps a camp of monsters alive around this point. Spawns them when the server starts
    /// and brings a new one some time after each one dies. Does nothing on clients.
    /// The monster prefab must be a registered network prefab.
    /// </summary>
    public class MonsterSpawner : MonoBehaviour
    {
        [SerializeField] private GameObject monsterPrefab;
        [SerializeField, Min(0)] private int count = 3;
        [SerializeField, Min(0f)] private float radius = 1.2f;
        [SerializeField, Min(0f)] private float respawnSeconds = 20f;

        private readonly List<NetworkObject> alive = new List<NetworkObject>();
        private readonly List<float> respawnTimes = new List<float>();
        private bool running;

        /// <summary>Server only: the monsters of this camp that are currently spawned.</summary>
        public IReadOnlyList<NetworkObject> Alive => alive;

        public float RespawnSeconds
        {
            get => respawnSeconds;
            set => respawnSeconds = Mathf.Max(0f, value);
        }

        /// <summary>Sets what and how many to spawn (used by the scene builder and tests).</summary>
        public void Configure(GameObject prefab, int monsterCount, float spawnRadius, float respawnDelay)
        {
            monsterPrefab = prefab;
            count = Mathf.Max(0, monsterCount);
            radius = Mathf.Max(0f, spawnRadius);
            respawnSeconds = Mathf.Max(0f, respawnDelay);
        }

        private void Update()
        {
            NetworkManager network = NetworkManager.Singleton;
            bool isServer = network != null && network.IsListening && network.IsServer;
            if (!isServer)
            {
                if (running) StopCamp();
                return;
            }

            if (!running)
            {
                running = true;
                for (int i = 0; i < count; i++)
                    Spawn();
                return;
            }

            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (alive[i] != null && alive[i].IsSpawned) continue;
                alive.RemoveAt(i);
                respawnTimes.Add(Time.time + respawnSeconds);
            }

            for (int i = respawnTimes.Count - 1; i >= 0; i--)
            {
                if (Time.time < respawnTimes[i]) continue;
                respawnTimes.RemoveAt(i);
                Spawn();
            }
        }

        private void StopCamp()
        {
            // The network shutdown already destroyed the spawned monsters.
            running = false;
            alive.Clear();
            respawnTimes.Clear();
        }

        private void Spawn()
        {
            if (monsterPrefab == null) return;

            Vector2 center = transform.position;
            WorldNavigation navigation = WorldNavigation.Instance;
            Vector2 point;
            if (navigation == null || !navigation.TryGetRandomPoint(center, radius, out point))
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                point = center + new Vector2(offset.x, offset.y * 0.5f);
            }

            GameObject monster = Instantiate(monsterPrefab, point, Quaternion.identity);
            if (monster.TryGetComponent(out MonsterAI ai)) ai.Home = point;

            var networkObject = monster.GetComponent<NetworkObject>();
            networkObject.Spawn(destroyWithScene: true);
            alive.Add(networkObject);
        }
    }
}
