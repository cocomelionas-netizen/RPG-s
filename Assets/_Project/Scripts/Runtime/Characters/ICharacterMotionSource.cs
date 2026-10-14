using UnityEngine;

namespace RPG.Characters
{
    /// <summary>
    /// Anything that moves a character (player input, monster AI, network replication)
    /// exposes its motion through this, so presentation code does not depend on who drives it.
    /// </summary>
    public interface ICharacterMotionSource
    {
        /// <summary>Last non-zero movement direction in screen space.</summary>
        Vector2 FacingDirection { get; }

        bool IsMoving { get; }
    }
}
