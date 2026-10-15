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
    /// Experience comes from monster kills (<see cref="ServerAddExperience"/>); levelling up raises stats and heals
    /// fully. Earned experience is also spent on attribute points, each costing more than the last
    /// (<see cref="RequestSpendPoint"/>, <see cref="AttributeRules.PointCost"/>). Damage, health, mana and critical chance
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
        // Experience earned and not yet spent on attributes (like Source: experience levels you up and is also
        // the currency for attribute points).
        private readonly NetworkVariable<int> experienceToSpend = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private static readonly Color LevelUpColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color ExperienceTextColor = new Color(0.75f, 0.55f, 1f);

        private Health health;
        private AutoAttack autoAttack;
        private CharacterAnimator characterAnimator;
        private Inventory inventory;
        private Mana mana;
        private HealthRegeneration regeneration;

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
        /// <summary>Experience that can still be spent on attribute points.</summary>
        public int ExperienceToSpend => experienceToSpend.Value;
        public AttributeSet SpentPoints => spentPoints.Value;

        /// <summary>Experience the next point in an attribute costs.</summary>
        public int PointCost(Attribute attribute) => AttributeRules.PointCost(attribute, spentPoints.Value[attribute]);

        public bool CanAfford(Attribute attribute) => experienceToSpend.Value >= PointCost(attribute);

        /// <summary>Class attributes plus spent points plus the equipment's.</summary>
        public AttributeSet TotalAttributes
        {
            get
            {
                AttributeSet total = (Class != null ? Class.baseAttributes : default) + spentPoints.Value;
                return inventory != null ? total + inventory.Attributes : total;
            }
        }

        /// <summary>Whether the class fights at range (its auto-attack shoots).</summary>
        public bool IsRanged => Class != null && Class.projectileColor.a > 0f;

        /// <summary>Critical hit chance of auto-attacks with Agility and the melee or ranged critical rating (0..1).</summary>
        public float CritChance
        {
            get
            {
                if (Class == null) return 0f;
                AttributeSet total = TotalAttributes;
                return Mathf.Clamp01(Class.critChance + AttributeRules.CritBonus(total) + AttributeRules.AttackCritBonus(total, IsRanged));
            }
        }

        /// <summary>Critical hit chance of abilities with Agility and Spell Critical (0..1).</summary>
        public float SpellCritChance
        {
            get
            {
                if (Class == null) return 0f;
                AttributeSet total = TotalAttributes;
                return Mathf.Clamp01(Class.critChance + AttributeRules.CritBonus(total) + AttributeRules.SpellCritBonus(total));
            }
        }

        /// <summary>The weapon skill of the worn weapon (null without a weapon or skill).</summary>
        public Attribute? WeaponSkill
        {
            get
            {
                ItemDefinition weapon = inventory != null ? inventory.Definition(inventory.GetEquipped(ItemSlot.Weapon)) : null;
                return weapon != null ? ItemDefinition.SkillOf(weapon.weaponType) : null;
            }
        }

        private void Awake()
        {
            health = GetComponent<Health>();
            autoAttack = GetComponent<AutoAttack>();
            characterAnimator = GetComponent<CharacterAnimator>();
            inventory = GetComponent<Inventory>();
            mana = GetComponent<Mana>();
            regeneration = GetComponent<HealthRegeneration>();
        }

        public override void OnNetworkSpawn()
        {
            classIndex.OnValueChanged += OnClassChanged;
            level.OnValueChanged += OnLevelChanged;
            spentPoints.OnValueChanged += OnPointsChanged;
            experienceToSpend.OnValueChanged += OnExperienceToSpendChanged;
            if (inventory != null) inventory.Changed += OnEquipmentChanged;

            if (IsServer)
            {
                classIndex.Value = catalog != null && catalog.Get(defaultClass) != null ? defaultClass : 0;
                level.Value = 1;
                experience.Value = 0;
                spentPoints.Value = default;
                experienceToSpend.Value = 0;
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
            experienceToSpend.OnValueChanged -= OnExperienceToSpendChanged;
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

        private void OnExperienceToSpendChanged(int previous, int current)
        {
            Changed?.Invoke();
        }

        // ---------- Server ----------

        /// <summary>
        /// Server only: adds experience, levelling up as many times as it covers. The same experience can also be
        /// spent on attribute points (spending it does not lower the level).
        /// </summary>
        public void ServerAddExperience(int amount)
        {
            if (!IsServer || amount <= 0) return;

            int newLevel = level.Value;
            int newExperience = experience.Value;
            LevelCurve.Gain(ref newLevel, ref newExperience, amount);

            experience.Value = newExperience;
            experienceToSpend.Value = (int)Mathf.Min(int.MaxValue, (long)experienceToSpend.Value + amount);
            ExperienceGainedRpc(amount);
            if (newLevel != level.Value)
            {
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

        /// <summary>
        /// Server only: restores a saved character (class, level, progress into the level, attribute points and
        /// the experience still to spend).
        /// </summary>
        public void ServerLoad(int savedClass, int savedLevel, int savedExperience, AttributeSet savedSpent, int savedExperienceToSpend)
        {
            if (!IsServer) return;
            if (catalog != null && catalog.Get(savedClass) != null) classIndex.Value = savedClass;
            level.Value = Mathf.Clamp(savedLevel, 1, LevelCurve.MaxLevel);
            experience.Value = Mathf.Clamp(savedExperience, 0, Mathf.Max(0, LevelCurve.ExperienceToNext(level.Value) - 1));

            AttributeSet spent = default;
            for (int i = 0; i < AttributeSet.Count; i++)
                spent[(Attribute)i] = Mathf.Max(0, savedSpent[(Attribute)i]);
            spentPoints.Value = spent;
            experienceToSpend.Value = Mathf.Max(0, savedExperienceToSpend);
            ApplyClassAndLevel(true);
        }

        /// <summary>Owner: buys one point in an attribute with experience.</summary>
        public void RequestSpendPoint(Attribute attribute)
        {
            if (IsOwner) SpendPointRpc(attribute);
        }

        [Rpc(SendTo.Server)]
        private void SpendPointRpc(Attribute attribute, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerSpendPoint(attribute);
        }

        /// <summary>Server only: buys one point in an attribute at <see cref="PointCost"/>. False when the experience does not cover it.</summary>
        public bool ServerSpendPoint(Attribute attribute)
        {
            if (!IsServer || attribute < 0 || (int)attribute >= AttributeSet.Count) return false;
            int cost = PointCost(attribute);
            if (experienceToSpend.Value < cost) return false;
            AttributeSet spent = spentPoints.Value;
            spent[attribute] += 1;
            experienceToSpend.Value -= cost;
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
                MessageLog.Add($"You reached level {newLevel} and the rank of {LevelCurve.RankName(newLevel)}!", LevelUpColor);
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
            // The worn weapon's skill (Swords, Ranged, Staves...) raises all its damage.
            float skill = AttributeRules.WeaponSkillBonus(attributes, WeaponSkill);
            autoAttack.SetStats(Mathf.RoundToInt((definition.MinDamageAt(currentLevel) + damageBonus) * skill),
                Mathf.RoundToInt((definition.MaxDamageAt(currentLevel) + damageBonus) * skill),
                definition.attackInterval, definition.attackRange, CritChance, definition.projectileColor);
            if (characterAnimator != null && definition.animations != null)
                characterAnimator.AnimationSet = definition.animations;
            if (IsServer && IsSpawned)
            {
                health.ServerSetMaxHealth(definition.MaxHealthAt(currentLevel) + healthBonus, healFully);
                health.ServerArmor = inventory != null ? inventory.Armor : 0;
                health.ServerDodgeChance = AttributeRules.DodgeChance(attributes);
                health.ServerBlockChance = AttributeRules.BlockChance(attributes);
                health.ServerSetResist(DamageSchool.Frost, attributes.resistFrost);
                health.ServerSetResist(DamageSchool.Fire, attributes.resistFire);
                health.ServerSetResist(DamageSchool.Shadow, attributes.resistShadow);
                health.ServerSetResist(DamageSchool.Holy, attributes.resistHoly);
                autoAttack.SpellCritChance = SpellCritChance;
                autoAttack.AbilityPower = AttributeRules.AbilityPower(attributes);
                if (regeneration != null) regeneration.ServerBonusPerSecond = AttributeRules.HealthRegenPerSecond(attributes);
                if (mana != null) mana.ServerBonusPerSecond = AttributeRules.ManaRegenPerSecond(attributes);
                if (mana != null) mana.ServerSetMax(definition.MaxManaAt(currentLevel) + manaBonus, healFully);
            }

            Changed?.Invoke();
        }
    }
}
