using System.Collections.Generic;
using RPG.Navigation;
using RPG.Combat;
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
        [Tooltip("Chance that a spawned monster is an elite (tougher, better rewards).")]
        [SerializeField, Range(0f, 1f)] private float eliteChance = 0.08f;
        [Tooltip("The level of the monsters at this spot (0 keeps the prefab's).")]
        [SerializeField, Min(0)] private int level;
        [Tooltip("The school of the monsters' hits here (physical keeps the prefab's).")]
        [SerializeField] private DamageSchool damageSchool = DamageSchool.Physical;
        [Tooltip("The name of the monsters here (empty keeps the prefab's).")]
        [SerializeField] private string displayName;
        [Tooltip("Rolled once when one of them dies, on top of its own drops (keys).")]
        [SerializeField] private RPG.Items.LootEntry[] bonusLoot = System.Array.Empty<RPG.Items.LootEntry>();

        private readonly List<NetworkObject> alive = new List<NetworkObject>();
        private readonly List<float> respawnTimes = new List<float>();
        private bool running;

        /// <summary>Server only: the monsters of this camp that are currently spawned.</summary>
        public IReadOnlyList<NetworkObject> Alive => alive;

        public float EliteChance
        {
            get => eliteChance;
            set => eliteChance = Mathf.Clamp01(value);
        }

        /// <summary>The level of the monsters at this spot (0 keeps the prefab's).</summary>
        public int Level
        {
            get => level;
            set => level = Mathf.Max(0, value);
        }

        /// <summary>The school of the monsters' hits at this spot (physical keeps the prefab's).</summary>
        public DamageSchool School
        {
            get => damageSchool;
            set => damageSchool = value;
        }

        /// <summary>The name of the monsters at this spot (empty keeps the prefab's).</summary>
        public string DisplayName
        {
            get => displayName;
            set => displayName = value;
        }

        /// <summary>Drops rolled once when a monster of this spot dies, on top of its own (keys).</summary>
        public RPG.Items.LootEntry[] BonusLoot
        {
            get => bonusLoot;
            set => bonusLoot = value ?? System.Array.Empty<RPG.Items.LootEntry>();
        }

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
            WorldNavigation navigation = WorldNavigation.At(center);
            Vector2 point;
            if (navigation == null || !navigation.TryGetRandomPoint(center, radius, out point))
            {
                Vector2 offset = Random.insideUnitCircle * radius;
                point = center + new Vector2(offset.x, offset.y * 0.5f);
            }

            GameObject monster = Instantiate(monsterPrefab, point, Quaternion.identity);
            if (monster.TryGetComponent(out MonsterAI ai))
            {
                ai.Home = point;
                if (level > 0) ai.ServerOverrideLevel(level);
                if (bonusLoot.Length > 0) ai.ServerAddBonusLoot(bonusLoot);
            }
            if (damageSchool != DamageSchool.Physical && monster.TryGetComponent(out AutoAttack attack)) attack.School = damageSchool;

            var networkObject = monster.GetComponent<NetworkObject>();
            networkObject.Spawn(destroyWithScene: true);
            if (ai != null && Random.value < eliteChance) ai.ServerMakeElite();
            if (!string.IsNullOrEmpty(displayName) && monster.TryGetComponent(out Health health)) health.ServerSetDisplayName(displayName);
            alive.Add(networkObject);
        }
    }
}
