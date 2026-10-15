using System.Collections.Generic;
using RPG.Combat;
using RPG.Items;
using RPG.Navigation;
using RPG.Progression;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Monsters
{
    public enum MonsterState
    {
        /// <summary>Wanders around home, watching for players.</summary>
        Idle,
        /// <summary>Chases and attacks the player with the most threat.</summary>
        Chase,
        /// <summary>Gave up: walks home, ignoring damage, and returns to full health there.</summary>
        Evade,
        /// <summary>Lies dead until the corpse is removed.</summary>
        Dead,
    }

    /// <summary>
    /// Server-side monster brain.
    /// - Idle: wanders near home; a hostile player coming within the aggro radius (or hitting it) starts a fight.
    /// - Chase: keeps a threat table (damage dealt to it, plus a little for being seen), attacks the top entry
    ///   with <see cref="AutoAttack"/> and calls nearby idle monsters to help.
    /// - Leash: it chases up to <see cref="LeashRadius"/> from where the fight started, and every hit it lands
    ///   moves that point to where it hit, so a monster that keeps hitting you keeps following you.
    /// - Give up: when it has not landed a hit for <see cref="ChaseTimeout"/> seconds (the player keeps running).
    /// - Evade: when pulled past the leash, when it gave up, when its targets are dead or gone, or when it cannot reach them,
    ///   it walks home ignoring damage and returns to full health (no kiting monsters across the map).
    /// - Dead: every living player that fought it gets experience; the corpse is removed after a short while
    ///   and a <see cref="MonsterSpawner"/> brings a new one later.
    /// Clients never run this; they only see the replicated movement, health and attack animations.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(AutoAttack), typeof(MonsterMotion))]
    public class MonsterAI : NetworkBehaviour
    {
        [Header("Reward")]
        [SerializeField, Min(1)] private int level = 1;
        [Tooltip("Experience for each player who fought it (less for players above its level).")]
        [SerializeField, Min(0)] private int experienceReward = 20;
        [Tooltip("Gold for each player who fought it (min, max).")]
        [SerializeField] private Vector2Int goldReward = new Vector2Int(2, 5);
        [Tooltip("Each entry is rolled on death; the items that drop are left in a loot bag.")]
        [SerializeField] private LootEntry[] loot = System.Array.Empty<LootEntry>();
        [SerializeField] private GameObject lootBagPrefab;
        [Tooltip("Rolls random affixes on dropped weapons and armor (none when empty).")]
        [SerializeField] private AffixCatalog affixes;
        [Tooltip("Gear dropped by level: weapons and armor from here around the monster's level (none when empty).")]
        [SerializeField] private ItemCatalog gear;
        [Tooltip("Chance that a dropped weapon or armor piece has a socket for a gem.")]
        [SerializeField, Range(0f, 1f)] private float socketChance = 0.25f;
        [Tooltip("Bosses: pieces of gear always dropped, with an affix of the quality below (Source's bosses always drop gold).")]
        [SerializeField, Min(0)] private int guaranteedGear;
        [SerializeField] private ItemRarity guaranteedQuality = ItemRarity.Rare;
        [Tooltip("Bosses: chance of one more piece of the top quality (Source's purple: legendary).")]
        [SerializeField, Range(0f, 1f)] private float purpleChance;

        [Header("Aggro")]
        [SerializeField, Min(0f)] private float aggroRadius = 3f;
        [Tooltip("Idle monsters this close join the fight when this one is pulled.")]
        [SerializeField, Min(0f)] private float assistRadius = 2.5f;
        [Tooltip("Gives up the chase this far from home, or from where it last hit its target.")]
        [SerializeField, Min(1f)] private float leashRadius = 25f;
        [Tooltip("Gives up and walks home when it has not hit its target for this many seconds.")]
        [SerializeField, Min(1f)] private float chaseTimeout = 12f;
        [Tooltip("Walks home when it cannot find a way to its target for this long.")]
        [SerializeField, Min(0.5f)] private float giveUpSeconds = 3f;

        [Header("Idle")]
        [SerializeField, Min(0f)] private float wanderRadius = 1.2f;
        [SerializeField] private Vector2 wanderPauseSeconds = new Vector2(3f, 8f);
        [SerializeField, Range(0.1f, 1f)] private float wanderSpeedScale = 0.5f;

        [Header("Evade")]
        [SerializeField, Min(1f)] private float evadeSpeedScale = 1.5f;

        [Header("Death")]
        [SerializeField, Min(0f)] private float corpseSeconds = 3f;

        private const float ScanInterval = 0.25f;
        private const float SightThreat = 1f;
        private const float AssistThreat = 0.5f;
        private const float SpreadDistance = 0.3f;
        private const float HomeReachedDistance = 0.15f;
        // The path home may end next to home when home itself is blocked.
        private const float HomeNearbyDistance = 1f;
        // Evading longer than this means it got stuck somewhere: it is placed back home.
        private const float MaxEvadeSeconds = 30f;

        private static readonly List<MonsterAI> ServerMonsters = new List<MonsterAI>();

        private readonly Dictionary<Health, float> threat = new Dictionary<Health, float>();
        private readonly List<Health> expiredThreat = new List<Health>();

        private Health health;
        private AutoAttack autoAttack;
        private MonsterMotion motion;
        private Vector2 home;
        private bool hasHome;
        private float nextScanTime;
        private float nextWanderTime;
        private float pathFailedSince = -1f;
        private float removeAt;
        private float evadeStartedAt;
        private Vector2 leashAnchor;
        private float lastHitTime;

        /// <summary>Server only.</summary>
        public MonsterState State { get; private set; } = MonsterState.Idle;

        /// <summary>Server only: who it is fighting.</summary>
        public Health CurrentTarget { get; private set; }

        /// <summary>
        /// Where it spawned and returns to. Set it before spawning to choose another point;
        /// setting it also measures the leash of a fight in progress from there.
        /// </summary>
        public Vector2 Home
        {
            get => home;
            set
            {
                home = value;
                hasHome = true;
                leashAnchor = value;
            }
        }

        // The level every peer sees (a spawn spot can give a monster another level than its prefab's).
        private readonly NetworkVariable<int> networkLevel = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private int prefabLevel;

        /// <summary>Shown on the target frame and nameplate; sets the experience it gives and the level of its gear drops.</summary>
        public int Level => IsSpawned && networkLevel.Value > 0 ? networkLevel.Value : level;

        /// <summary>
        /// Server, before spawning: gives this monster another level (a Source spawn spot's), with the experience
        /// a kill of that level is worth.
        /// </summary>
        public void ServerOverrideLevel(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, LevelCurve.MaxLevel);
            experienceReward = LevelCurve.KillExperienceFor(level);
        }

        // How much tougher a monster gets per level. Its damage grows as fast as players' health does
        // (LevelCurve.HealthGrowth, Source's), so a monster of your level is as dangerous at 25 as at 1.
        private const float HealthPerLevel = 0.15f;

        /// <summary>How much more health a monster of <paramref name="toLevel"/> has than the same monster of <paramref name="fromLevel"/>.</summary>
        public static float HealthScaleBetween(int fromLevel, int toLevel) =>
            (1f + HealthPerLevel * (toLevel - 1)) / (1f + HealthPerLevel * (fromLevel - 1));

        public static float DamageScaleBetween(int fromLevel, int toLevel) =>
            LevelCurve.HealthGrowth(toLevel) / LevelCurve.HealthGrowth(fromLevel);

        protected override void OnNetworkPostSpawn()
        {
            // After Health has set itself up: a monster given another level than its prefab's is as tough as that level.
            if (!IsServer || level == prefabLevel) return;
            health.ServerSetMaxHealth(Mathf.Max(1, Mathf.RoundToInt(health.Max * HealthScaleBetween(prefabLevel, level))), true);
            autoAttack.DamageScale *= DamageScaleBetween(prefabLevel, level);
        }

        // ---------- Elites ----------

        // Source's elites have about 5.6 times the health of normal monsters.
        public const float EliteHealthScale = 5.6f;
        public const float EliteDamageScale = 1.5f;
        public const int EliteRewardScale = 3;
        /// <summary>Extra rolls of the material chance for an elite, and for a boss (a monster with guaranteed gear).</summary>
        public const int EliteMaterialRolls = 3;
        public const int BossMaterialRolls = 6;
        private const float EliteSizeScale = 1.25f;

        private readonly NetworkVariable<bool> elite = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private Vector3 normalScale;

        /// <summary>A rare, stronger version of the monster: bigger, tougher, hits harder, rewards more.</summary>
        public bool IsElite => elite.Value;

        /// <summary>Server, after spawning: turns this monster into an elite.</summary>
        public void ServerMakeElite()
        {
            if (!IsServer || !IsSpawned || elite.Value) return;
            elite.Value = true;
            health.ServerSetDisplayName($"Elite {health.DisplayName}");
            health.ServerSetMaxHealth(Mathf.RoundToInt(health.Max * EliteHealthScale), true);
            autoAttack.DamageScale *= EliteDamageScale;
        }

        private void OnEliteChanged(bool previous, bool current) => ApplyEliteLook();

        private void ApplyEliteLook()
        {
            transform.localScale = normalScale * (elite.Value ? EliteSizeScale : 1f);
        }

        public int ExperienceReward => experienceReward;

        public IReadOnlyList<LootEntry> Loot => loot;

        public Vector2Int GoldReward
        {
            get => goldReward;
            set => goldReward = new Vector2Int(Mathf.Max(0, value.x), Mathf.Max(value.x, value.y));
        }

        /// <summary>Sets the drops (used by the scene builder and tests).</summary>
        public void ConfigureLoot(GameObject bagPrefab, LootEntry[] entries, AffixCatalog affixCatalog = null, ItemCatalog gearCatalog = null)
        {
            lootBagPrefab = bagPrefab;
            loot = entries ?? System.Array.Empty<LootEntry>();
            if (affixCatalog != null) affixes = affixCatalog;
            if (gearCatalog != null) gear = gearCatalog;
        }

        public float AggroRadius
        {
            get => aggroRadius;
            set => aggroRadius = Mathf.Max(0f, value);
        }

        /// <summary>Server only: where the leash is measured from (home, or where it last hit its target).</summary>
        public Vector2 LeashAnchor => State == MonsterState.Chase ? leashAnchor : home;

        public float ChaseTimeout
        {
            get => chaseTimeout;
            set => chaseTimeout = Mathf.Max(1f, value);
        }

        public float LeashRadius
        {
            get => leashRadius;
            set => leashRadius = Mathf.Max(1f, value);
        }

        public float CorpseSeconds
        {
            get => corpseSeconds;
            set => corpseSeconds = Mathf.Max(0f, value);
        }

        private Vector2 Position => transform.position;

        private void Awake()
        {
            health = GetComponent<Health>();
            autoAttack = GetComponent<AutoAttack>();
            motion = GetComponent<MonsterMotion>();
            normalScale = transform.localScale;
            prefabLevel = level;
        }

        public override void OnNetworkSpawn()
        {
            elite.OnValueChanged += OnEliteChanged;
            ApplyEliteLook();
            if (!IsServer) return;
            networkLevel.Value = level;

            if (!hasHome) Home = transform.position;
            health.Died += OnDied;
            health.DamageTaken += OnDamageTaken;
            autoAttack.ServerHitLanded += OnHitLanded;
            ServerMonsters.Add(this);
            nextWanderTime = Time.time + Random.Range(wanderPauseSeconds.x, wanderPauseSeconds.y);
        }

        public override void OnNetworkDespawn()
        {
            elite.OnValueChanged -= OnEliteChanged;
            if (!IsServer) return;

            health.Died -= OnDied;
            health.DamageTaken -= OnDamageTaken;
            autoAttack.ServerHitLanded -= OnHitLanded;
            ServerMonsters.Remove(this);
        }

        private void Update()
        {
            if (!IsServer || !IsSpawned) return;

            switch (State)
            {
                case MonsterState.Dead:
                    if (Time.time >= removeAt) NetworkObject.Despawn(true);
                    return;
                case MonsterState.Evade:
                    UpdateEvade();
                    return;
            }

            if (Vector2.Distance(Position, LeashAnchor) > leashRadius)
            {
                StartEvade();
                return;
            }

            if (Time.time >= nextScanTime)
            {
                nextScanTime = Time.time + ScanInterval;
                ScanForPlayers();
            }

            CurrentTarget = TopThreat();
            if (CurrentTarget != null)
            {
                if (State != MonsterState.Chase)
                {
                    leashAnchor = home;
                    lastHitTime = Time.time;
                }
                State = MonsterState.Chase;

                // The player got away: no hit for a while.
                if (Time.time - lastHitTime >= chaseTimeout)
                {
                    StartEvade();
                    return;
                }
                UpdateChase();
            }
            else if (State == MonsterState.Chase)
            {
                // Everyone it fought is dead or gone.
                StartEvade();
            }
            else
            {
                UpdateIdle();
            }
        }

        // ---------- Threat ----------

        private void ScanForPlayers()
        {
            foreach (NetworkClient client in NetworkManager.ConnectedClientsList)
            {
                NetworkObject player = client.PlayerObject;
                if (player == null || !player.TryGetComponent(out Health playerHealth)) continue;
                if (playerHealth.IsDead || !health.IsHostileTo(playerHealth)) continue;

                if (Vector2.Distance(Position, AutoAttack.ServerPosition(playerHealth)) <= aggroRadius)
                    AddThreat(playerHealth, SightThreat * ScanInterval, true);
            }
        }

        /// <summary>Server only: makes the monster angrier at someone (and starts a fight when idle).</summary>
        public void AddThreat(Health source, float amount, bool callForHelp)
        {
            if (!IsServer || source == null || State == MonsterState.Evade || State == MonsterState.Dead) return;

            bool isNew = !threat.TryGetValue(source, out float current);
            threat[source] = current + amount;
            if (!isNew || !callForHelp) return;

            foreach (MonsterAI other in ServerMonsters)
            {
                if (other == this || other.State != MonsterState.Idle) continue;
                if (Vector2.Distance(other.Position, Position) <= assistRadius)
                    other.AddThreat(source, AssistThreat, false);
            }
        }

        private void OnHitLanded(Health target, int damage)
        {
            // Still in the fight: it may follow its target another leash length from here.
            if (State != MonsterState.Chase) return;
            leashAnchor = Position;
            lastHitTime = Time.time;
        }

        private void OnDamageTaken(Health attacker, int amount)
        {
            if (attacker != null) AddThreat(attacker, amount, true);
        }

        private Health TopThreat()
        {
            Health best = null;
            float bestThreat = float.MinValue;
            expiredThreat.Clear();

            foreach (KeyValuePair<Health, float> entry in threat)
            {
                Health unit = entry.Key;
                bool gone = unit == null || !unit.IsSpawned || unit.IsDead
                            || Vector2.Distance(Position, AutoAttack.ServerPosition(unit)) > leashRadius + aggroRadius;
                if (gone)
                {
                    expiredThreat.Add(unit);
                    continue;
                }

                if (entry.Value > bestThreat)
                {
                    best = unit;
                    bestThreat = entry.Value;
                }
            }

            foreach (Health unit in expiredThreat)
                threat.Remove(unit);
            return best;
        }

        private void DropLoot()
        {
            // Gear is crafted from the materials monsters drop: elites roll the table twice and drop more
            // materials; bosses drop the most, plus their guaranteed finished gear (gold, sometimes purple).
            var guaranteed = new List<ItemRarity>();
            for (int i = 0; i < guaranteedGear; i++) guaranteed.Add(guaranteedQuality);
            if (purpleChance > 0f && Random.value < purpleChance) guaranteed.Add(ItemRarity.Legendary);
            int extraMaterialRolls = (IsElite ? EliteMaterialRolls : 0) + (guaranteedGear > 0 ? BossMaterialRolls : 0);
            LootDrop.Spawn(lootBagPrefab, transform.position, loot, gear, affixes, level, socketChance, IsElite ? 2 : 1,
                extraMaterialRolls, guaranteed.ToArray());
        }

        /// <summary>Sets the gear a boss always drops (used by the scene builder).</summary>
        public void ConfigureGuaranteedLoot(int pieces, ItemRarity quality, float extraPurpleChance)
        {
            guaranteedGear = Mathf.Max(0, pieces);
            guaranteedQuality = quality;
            purpleChance = Mathf.Clamp01(extraPurpleChance);
        }

        private void AwardExperience()
        {
            foreach (Health fighter in threat.Keys)
            {
                if (fighter == null || !fighter.IsSpawned || fighter.IsDead) continue;
                int rewardScale = IsElite ? EliteRewardScale : 1;
                if (experienceReward > 0 && fighter.TryGetComponent(out CharacterProgress progress))
                    progress.ServerAddExperience(LevelCurve.KillExperience(experienceReward * rewardScale, level, progress.Level));
                if (goldReward.y > 0 && fighter.TryGetComponent(out Inventory inventory))
                    inventory.ServerAddGold(Random.Range(goldReward.x, goldReward.y + 1) * rewardScale);
            }
        }

        // ---------- States ----------

        private void UpdateIdle()
        {
            if (wanderRadius <= 0f || motion.Destination.HasValue || Time.time < nextWanderTime) return;

            nextWanderTime = Time.time + Random.Range(wanderPauseSeconds.x, wanderPauseSeconds.y);
            WorldNavigation navigation = WorldNavigation.At(home);
            Vector2 point;
            if (navigation == null)
            {
                Vector2 offset = Random.insideUnitCircle * wanderRadius;
                point = home + new Vector2(offset.x, offset.y * 0.5f);
            }
            else if (!navigation.TryGetRandomPoint(home, wanderRadius, out point))
            {
                return;
            }
            motion.ServerMoveTo(point, wanderSpeedScale);
        }

        private void UpdateChase()
        {
            if (autoAttack.ServerTarget != CurrentTarget)
                autoAttack.ServerStartAttack(CurrentTarget);

            Vector2 self = Position;
            Vector2 target = AutoAttack.ServerPosition(CurrentTarget);
            if (Vector2.Distance(self, target) > autoAttack.Range * 0.85f)
            {
                motion.ServerMoveTo(target);
                if (motion.PathFailed)
                {
                    if (pathFailedSince < 0f) pathFailedSince = Time.time;
                    if (Time.time - pathFailedSince >= giveUpSeconds) StartEvade();
                    return;
                }
            }
            else
            {
                if (motion.Destination.HasValue) motion.ServerStop();
                motion.ServerFace(target - self);
                SpreadOut();
            }
            pathFailedSince = -1f;
        }

        /// <summary>Steps away from other monsters standing on the same spot, so a pack surrounds its target.</summary>
        private void SpreadOut()
        {
            Vector2 push = Vector2.zero;
            foreach (MonsterAI other in ServerMonsters)
            {
                if (other == this || other.State == MonsterState.Dead) continue;
                Vector2 away = Position - other.Position;
                float distance = away.magnitude;
                if (distance >= SpreadDistance) continue;

                // Identical positions: push in a direction that differs per monster.
                if (distance < 0.001f) away = Quaternion.Euler(0f, 0f, NetworkObjectId * 47f) * Vector2.right;
                push += away.normalized * (SpreadDistance - distance);
            }

            float maxStep = motion.MoveSpeed * 0.5f * Time.deltaTime;
            motion.ServerNudge(Vector2.ClampMagnitude(push, maxStep));
        }

        private void StartEvade()
        {
            State = MonsterState.Evade;
            CurrentTarget = null;
            threat.Clear();
            pathFailedSince = -1f;
            autoAttack.ServerStopAttack(false);
            health.ServerInvulnerable = true;
            evadeStartedAt = Time.time;
            motion.ServerStop();
            motion.ServerMoveTo(home, evadeSpeedScale);
        }

        private void UpdateEvade()
        {
            float distance = Vector2.Distance(Position, home);
            bool arrived = distance <= HomeReachedDistance || (!motion.Destination.HasValue && distance <= HomeNearbyDistance);
            if (!arrived)
            {
                // Keep steering home every frame, so a route that ended elsewhere is planned again.
                motion.ServerMoveTo(home, evadeSpeedScale);
                bool stuck = Time.time - evadeStartedAt >= MaxEvadeSeconds;
                if (!motion.PathFailed && !stuck) return;

                // No way home or stuck (should not happen on a sane map): jump there instead.
                motion.ServerTeleport(home);
            }

            motion.ServerStop();
            health.ServerInvulnerable = false;
            health.RestoreFull();
            State = MonsterState.Idle;
            nextWanderTime = Time.time + Random.Range(wanderPauseSeconds.x, wanderPauseSeconds.y);
        }

        private void OnDied()
        {
            AwardExperience();
            DropLoot();
            State = MonsterState.Dead;
            CurrentTarget = null;
            threat.Clear();
            removeAt = Time.time + corpseSeconds;
            autoAttack.ServerStopAttack(false);
            motion.ServerStop();
        }
    }
}
