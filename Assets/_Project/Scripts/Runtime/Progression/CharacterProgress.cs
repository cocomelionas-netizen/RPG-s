using System;
using RPG.Characters;
using RPG.Combat;
using RPG.Items;
using RPG.UI;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Progression
{
    /// <summary>
    /// A player's class, level and experience. The server owns all three; every peer derives the stats
    /// (max health, auto-attack damage, range, speed, plus equipment bonuses) and the look from them, so the owner also knows
    /// how close it must walk to attack.
    /// Experience comes from monster kills (<see cref="ServerAddExperience"/>); levelling up raises stats, heals fully
    /// and gives attribute points to spend (<see cref="RequestSpendPoint"/>). Damage, health, mana and critical chance
    /// come from the class, the level, the attributes (class + spent + items) and the equipment.
    /// The class is chosen when the character is created and restored with the rest of the character by
    /// <see cref="RPG.Accounts.CharacterPersistence"/>.
    /// </summary>
    [RequireComponent(typeof(Health), typeof(AutoAttack))]
    public class CharacterProgress : NetworkBehaviour
    {
        [SerializeField] private ClassCatalog catalog;
        [SerializeField, Min(0)] private int defaultClass;

        private readonly NetworkVariable<int> classIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> level = new NetworkVariable<int>(
            1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> experience = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<AttributeSet> spentPoints = new NetworkVariable<AttributeSet>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<int> unspentPoints = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private static readonly Color LevelUpColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color ExperienceTextColor = new Color(0.75f, 0.55f, 1f);

        private Health health;
        private AutoAttack autoAttack;
        private CharacterAnimator characterAnimator;
        private Inventory inventory;
        private Mana mana;

        /// <summary>Raised on every peer when the level goes up by gaining experience (not when a saved character is loaded).</summary>
        public event Action<int> LevelledUp;

        /// <summary>Raised on every peer when class or level changed (stats were re-applied).</summary>
        public event Action Changed;

        public ClassCatalog Catalog => catalog;
        public int ClassIndex => classIndex.Value;
        public CharacterClassDefinition Class => catalog != null ? catalog.Get(classIndex.Value) : null;
        public string ClassName => Class != null ? Class.displayName : "";
        public int Level => level.Value;
        public int Experience => experience.Value;
        public int ExperienceToNext => LevelCurve.ExperienceToNext(level.Value);
        public int UnspentPoints => unspentPoints.Value;
        public AttributeSet SpentPoints => spentPoints.Value;

        /// <summary>Class attributes plus spent points plus the equipment's.</summary>
        public AttributeSet TotalAttributes
        {
            get
            {
                AttributeSet total = (Class != null ? Class.baseAttributes : default) + spentPoints.Value;
                return inventory != null ? total + inventory.Attributes : total;
            }
        }

        /// <summary>Critical hit chance with Agility (0..1).</summary>
        public float CritChance => Class != null ? Mathf.Clamp01(Class.critChance + AttributeRules.CritBonus(TotalAttributes)) : 0f;

        private void Awake()
        {
            health = GetComponent<Health>();
            autoAttack = GetComponent<AutoAttack>();
            characterAnimator = GetComponent<CharacterAnimator>();
            inventory = GetComponent<Inventory>();
            mana = GetComponent<Mana>();
        }

        public override void OnNetworkSpawn()
        {
            classIndex.OnValueChanged += OnClassChanged;
            level.OnValueChanged += OnLevelChanged;
            spentPoints.OnValueChanged += OnPointsChanged;
            unspentPoints.OnValueChanged += OnUnspentChanged;
            if (inventory != null) inventory.Changed += OnEquipmentChanged;

            if (IsServer)
            {
                classIndex.Value = catalog != null && catalog.Get(defaultClass) != null ? defaultClass : 0;
                level.Value = 1;
                experience.Value = 0;
                spentPoints.Value = default;
                unspentPoints.Value = 0;
            }
        }

        protected override void OnNetworkPostSpawn()
        {
            // After every component spawned, so Health has set its own defaults first.
            ApplyClassAndLevel(true);
        }

        public override void OnNetworkDespawn()
        {
            classIndex.OnValueChanged -= OnClassChanged;
            level.OnValueChanged -= OnLevelChanged;
            spentPoints.OnValueChanged -= OnPointsChanged;
            unspentPoints.OnValueChanged -= OnUnspentChanged;
            if (inventory != null) inventory.Changed -= OnEquipmentChanged;
        }

        private void OnEquipmentChanged()
        {
            ApplyClassAndLevel(false);
        }

        private void OnPointsChanged(AttributeSet previous, AttributeSet current)
        {
            ApplyClassAndLevel(false);
        }

        private void OnUnspentChanged(int previous, int current)
        {
            Changed?.Invoke();
        }

        // ---------- Server ----------

        /// <summary>Server only: adds experience and levels up as many times as it covers.</summary>
        public void ServerAddExperience(int amount)
        {
            if (!IsServer || amount <= 0) return;

            int newLevel = level.Value;
            int newExperience = experience.Value;
            LevelCurve.Gain(ref newLevel, ref newExperience, amount);

            experience.Value = newExperience;
            ExperienceGainedRpc(amount);
            if (newLevel != level.Value)
            {
                unspentPoints.Value += (newLevel - level.Value) * AttributeRules.PointsPerLevel;
                level.Value = newLevel;
                LevelUpRpc(newLevel);
                Debug.Log($"[Server] {health.DisplayName} reached level {newLevel}.");
            }
        }

        /// <summary>Server only: switches class (keeps level and experience) and restores full health.</summary>
        public bool ServerSetClass(int index)
        {
            if (!IsServer || catalog == null || catalog.Get(index) == null) return false;
            if (classIndex.Value == index) return true;
            classIndex.Value = index;
            return true;
        }

        /// <summary>Server only: restores a saved character (class, level, progress into the level and attribute points).</summary>
        public void ServerLoad(int savedClass, int savedLevel, int savedExperience, AttributeSet savedSpent)
        {
            if (!IsServer) return;
            if (catalog != null && catalog.Get(savedClass) != null) classIndex.Value = savedClass;
            level.Value = Mathf.Clamp(savedLevel, 1, LevelCurve.MaxLevel);
            experience.Value = Mathf.Clamp(savedExperience, 0, Mathf.Max(0, LevelCurve.ExperienceToNext(level.Value) - 1));

            // Never more points than the level has given (a damaged or edited save).
            int earned = (level.Value - 1) * AttributeRules.PointsPerLevel;
            AttributeSet spent = default;
            for (int i = 0; i < AttributeSet.Count; i++)
                spent[(Attribute)i] = Mathf.Max(0, savedSpent[(Attribute)i]);
            int spentTotal = spent.Total;
            if (spentTotal > earned)
            {
                spent = default;
                spentTotal = 0;
            }
            spentPoints.Value = spent;
            // Every point the levels gave is either spent or waiting; characters saved before attributes get theirs now.
            unspentPoints.Value = earned - spentTotal;
            ApplyClassAndLevel(true);
        }

        /// <summary>Owner: puts one unspent point into an attribute.</summary>
        public void RequestSpendPoint(Attribute attribute)
        {
            if (IsOwner) SpendPointRpc(attribute);
        }

        [Rpc(SendTo.Server)]
        private void SpendPointRpc(Attribute attribute, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerSpendPoint(attribute);
        }

        /// <summary>Server only: spends one point on an attribute. False when there is none to spend.</summary>
        public bool ServerSpendPoint(Attribute attribute)
        {
            if (!IsServer || unspentPoints.Value <= 0 || attribute < Attribute.Strength || attribute > Attribute.Mana) return false;
            AttributeSet spent = spentPoints.Value;
            spent[attribute] += 1;
            unspentPoints.Value -= 1;
            spentPoints.Value = spent;
            return true;
        }

        [Rpc(SendTo.Owner)]
        private void ExperienceGainedRpc(int amount)
        {
            CombatTextLayer.ShowText(transform.position + Vector3.up * 1.05f, $"+{amount} XP", ExperienceTextColor, 0.8f);
            MessageLog.Add($"You gain {amount} experience.", ExperienceTextColor);
        }

        // ---------- Everyone ----------

        private void OnClassChanged(int previous, int current)
        {
            ApplyClassAndLevel(true);
        }

        private void OnLevelChanged(int previous, int current)
        {
            // Restoring a saved character also changes the level; only LevelUpRpc celebrates a real level up.
            ApplyClassAndLevel(current > previous);
        }

        [Rpc(SendTo.Everyone)]
        private void LevelUpRpc(int newLevel)
        {
            CombatEffects.Ring(transform.position, 0.9f, LevelUpColor);
            CombatTextLayer.ShowText(transform.position + Vector3.up * 1.2f, $"Level {newLevel}!", LevelUpColor, 1.3f);
            if (IsOwner)
                MessageLog.Add($"You reached level {newLevel} and the rank of {LevelCurve.RankName(newLevel)}! " +
                               $"You have {AttributeRules.PointsPerLevel} new attribute points (C).", LevelUpColor);
            LevelledUp?.Invoke(newLevel);
        }

        /// <summary>Applies the stats and look of the current class at the current level.</summary>
        private void ApplyClassAndLevel(bool healFully)
        {
            CharacterClassDefinition definition = Class;
            if (definition == null) return;

            int currentLevel = Mathf.Max(1, level.Value);
            AttributeSet attributes = TotalAttributes;
            int damageBonus = (inventory != null ? inventory.DamageBonus : 0) + AttributeRules.DamageBonus(attributes[definition.primaryAttribute]);
            int healthBonus = (inventory != null ? inventory.HealthBonus : 0) + AttributeRules.HealthBonus(attributes);
            int manaBonus = (inventory != null ? inventory.ManaBonus : 0) + AttributeRules.ManaBonus(attributes);
            autoAttack.SetStats(definition.MinDamageAt(currentLevel) + damageBonus, definition.MaxDamageAt(currentLevel) + damageBonus,
                definition.attackInterval, definition.attackRange, CritChance, definition.projectileColor);
            if (characterAnimator != null && definition.animations != null)
                characterAnimator.AnimationSet = definition.animations;
            if (IsServer && IsSpawned)
            {
                health.ServerSetMaxHealth(definition.MaxHealthAt(currentLevel) + healthBonus, healFully);
                health.ServerArmor = inventory != null ? inventory.Armor : 0;
                if (mana != null) mana.ServerSetMax(definition.MaxManaAt(currentLevel) + manaBonus, healFully);
            }

            Changed?.Invoke();
        }
    }
}
