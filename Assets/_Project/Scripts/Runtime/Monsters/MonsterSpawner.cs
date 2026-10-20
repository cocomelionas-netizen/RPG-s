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
    /// With an activation range the camp only exists while a player is near: its monsters appear when one comes within
    /// that range and the idle ones are taken away a while after every player has gone (a big world then only runs the
    /// monsters around its players).
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

        [Tooltip("The camp exists only while a player is this close (world units); 0: always.")]
        [SerializeField, Min(0f)] private float activationRange;

        /// <summary>Beyond the activation range by this much, for this long, the idle monsters are taken away.</summary>
        private const float DeactivationMargin = 6f;
        private const float DeactivationSeconds = 5f;
        private const float CheckInterval = 0.5f;

        private static readonly List<Vector2> PlayerPositions = new List<Vector2>();
        private static int playerPositionsFrame = -1;

        private readonly List<NetworkObject> alive = new List<NetworkObject>();
        private bool active;
        /// <summary>Monsters taken away while no player was near: they come back when one is.</summary>
        private int dormant;
        private float nextCheck;
        private float farSince = -1f;
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

        /// <summary>The camp exists only while a player is this close (world units); 0: always.</summary>
        public float ActivationRange
        {
            get => activationRange;
            set => activationRange = Mathf.Max(0f, value);
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
                active = activationRange <= 0f;
                if (active)
                {
                    for (int i = 0; i < count; i++)
                        Spawn();
                }
                else
                {
                    dormant = count;
                    // Spread the checks of the many camps over frames.
                    nextCheck = Time.time + Random.value * CheckInterval;
                }
                return;
            }

            if (activationRange > 0f && Time.time >= nextCheck)
            {
                nextCheck = Time.time + CheckInterval;
                UpdateActivation();
            }

            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (alive[i] != null && alive[i].IsSpawned) continue;
                alive.RemoveAt(i);
                respawnTimes.Add(Time.time + respawnSeconds);
            }

            if (!active) return;
            for (int i = respawnTimes.Count - 1; i >= 0; i--)
            {
                if (Time.time < respawnTimes[i]) continue;
                respawnTimes.RemoveAt(i);
                Spawn();
            }
        }

        private void UpdateActivation()
        {
            float nearest = NearestPlayerDistance(transform.position);
            if (nearest <= activationRange)
            {
                farSince = -1f;
                if (active) return;
                active = true;
                for (; dormant > 0; dormant--)
                    Spawn();
                return;
            }
            if (!active || nearest <= activationRange + DeactivationMargin) return;

            if (farSince < 0f) farSince = Time.time;
            if (Time.time - farSince < DeactivationSeconds) return;

            // Every player is far: the idle monsters go away (fighting ones stay until they are idle again).
            bool allGone = true;
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                NetworkObject monster = alive[i];
                if (monster == null || !monster.IsSpawned) continue;
                if (monster.TryGetComponent(out MonsterAI ai) && ai.State != MonsterState.Idle)
                {
                    allGone = false;
                    continue;
                }
                alive.RemoveAt(i);
                dormant++;
                monster.Despawn(true);
            }
            if (allGone) active = false;
        }

        private static float NearestPlayerDistance(Vector2 point)
        {
            if (playerPositionsFrame != Time.frameCount)
            {
                playerPositionsFrame = Time.frameCount;
                PlayerPositions.Clear();
                foreach (Health unit in Health.Spawned)
                    if (unit != null && unit.Faction == Faction.Player) PlayerPositions.Add(unit.transform.position);
            }
            float best = float.MaxValue;
            foreach (Vector2 position in PlayerPositions)
                best = Mathf.Min(best, Vector2.Distance(point, position));
            return best;
        }

        private void StopCamp()
        {
            // The network shutdown already destroyed the spawned monsters.
            running = false;
            active = false;
            dormant = 0;
            farSince = -1f;
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
