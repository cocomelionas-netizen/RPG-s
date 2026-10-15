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
    /// Experience comes from monster kills (<see cref="ServerAddExperience"/>); levelling up raises stats and heals fully.
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

        private static readonly Color LevelUpColor = new Color(1f, 0.85f, 0.3f);
        private static readonly Color ExperienceTextColor = new Color(0.75f, 0.55f, 1f);

        private Health health;
        private AutoAttack autoAttack;
        private CharacterAnimator characterAnimator;
        private Inventory inventory;

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

        private void Awake()
        {
            health = GetComponent<Health>();
            autoAttack = GetComponent<AutoAttack>();
            characterAnimator = GetComponent<CharacterAnimator>();
            inventory = GetComponent<Inventory>();
        }

        public override void OnNetworkSpawn()
        {
            classIndex.OnValueChanged += OnClassChanged;
            level.OnValueChanged += OnLevelChanged;
            if (inventory != null) inventory.Changed += OnEquipmentChanged;

            if (IsServer)
            {
                classIndex.Value = catalog != null && catalog.Get(defaultClass) != null ? defaultClass : 0;
                level.Value = 1;
                experience.Value = 0;
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
            if (inventory != null) inventory.Changed -= OnEquipmentChanged;
        }

        private void OnEquipmentChanged()
        {
            ApplyClassAndLevel(false);
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

        /// <summary>Server only: restores a saved character (class, level and progress into the level).</summary>
        public void ServerLoad(int savedClass, int savedLevel, int savedExperience)
        {
            if (!IsServer) return;
            if (catalog != null && catalog.Get(savedClass) != null) classIndex.Value = savedClass;
            level.Value = Mathf.Clamp(savedLevel, 1, LevelCurve.MaxLevel);
            experience.Value = Mathf.Clamp(savedExperience, 0, Mathf.Max(0, LevelCurve.ExperienceToNext(level.Value) - 1));
            ApplyClassAndLevel(true);
        }

        [Rpc(SendTo.Owner)]
        private void ExperienceGainedRpc(int amount)
        {
            CombatTextLayer.ShowText(transform.position + Vector3.up * 1.05f, $"+{amount} XP", ExperienceTextColor, 0.8f);
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
            LevelledUp?.Invoke(newLevel);
        }

        /// <summary>Applies the stats and look of the current class at the current level.</summary>
        private void ApplyClassAndLevel(bool healFully)
        {
            CharacterClassDefinition definition = Class;
            if (definition == null) return;

            int currentLevel = Mathf.Max(1, level.Value);
            int damageBonus = inventory != null ? inventory.DamageBonus : 0;
            int healthBonus = inventory != null ? inventory.HealthBonus : 0;
            autoAttack.SetStats(definition.MinDamageAt(currentLevel) + damageBonus, definition.MaxDamageAt(currentLevel) + damageBonus,
                definition.attackInterval, definition.attackRange, definition.critChance, definition.projectileColor);
            if (characterAnimator != null && definition.animations != null)
                characterAnimator.AnimationSet = definition.animations;
            if (IsServer && IsSpawned)
            {
                health.ServerSetMaxHealth(definition.MaxHealthAt(currentLevel) + healthBonus, healFully);
                health.ServerArmor = inventory != null ? inventory.Armor : 0;
            }

            Changed?.Invoke();
        }
    }
}
