using UnityEngine;

namespace RPG.Progression
{
    public enum AbilityKind
    {
        /// <summary>Damages the selected hostile target.</summary>
        TargetDamage,
        /// <summary>Damages every hostile unit within a radius around the selected target.</summary>
        TargetArea,
        /// <summary>Damages every hostile unit within a radius around the caster.</summary>
        SelfArea,
        /// <summary>Heals the caster by a share of its max health.</summary>
        SelfHeal,
    }

    /// <summary>
    /// One ability of a class (Heavy Strike, Fireball...). The server resolves it with
    /// <see cref="AbilityCaster"/>; damage is the caster's auto-attack damage times <see cref="damageMultiplier"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "RPG/Progression/Ability", fileName = "Ability")]
    public class AbilityDefinition : ScriptableObject
    {
        public string displayName = "Ability";
        public AbilityKind kind = AbilityKind.TargetDamage;
        [Min(0f)] public float cooldown = 6f;
        [Tooltip("Max distance to the target (target abilities).")]
        [Min(0f)] public float range = 1f;
        [Tooltip("Area radius (area abilities).")]
        [Min(0f)] public float radius = 1f;
        [Min(0f)] public float damageMultiplier = 2f;
        [Tooltip("Share of max health restored (heals).")]
        [Range(0f, 1f)] public float healFraction = 0.3f;
        [Tooltip("Color of the projectile, ring or glow shown when it is used.")]
        public Color effectColor = Color.white;

        public bool NeedsTarget => kind == AbilityKind.TargetDamage || kind == AbilityKind.TargetArea;
    }
}
