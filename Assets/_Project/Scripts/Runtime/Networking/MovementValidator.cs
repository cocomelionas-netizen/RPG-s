using UnityEngine;

namespace RPG.Networking
{
    /// <summary>
    /// Server-side speed check for client-reported positions, using a movement budget
    /// (token bucket): time earns distance at a slightly higher than normal speed, each move spends it.
    /// Network jitter that bunches several updates together is absorbed by the saved budget,
    /// while sustained speed hacks and teleports run out of budget and are rejected.
    /// </summary>
    public class MovementValidator
    {
        private readonly float maxSpeed;
        private readonly float maxBudget;
        private float budget;
        private double lastTime;

        /// <param name="moveSpeed">The character's normal speed in units per second.</param>
        /// <param name="speedTolerance">Allowed speed multiplier (1.25 = 25% faster than normal).</param>
        /// <param name="burstSeconds">How many seconds of movement can be saved up to absorb jitter.</param>
        public MovementValidator(float moveSpeed, double startTime, float speedTolerance = 1.25f, float burstSeconds = 0.5f)
        {
            maxSpeed = moveSpeed * speedTolerance;
            maxBudget = maxSpeed * burstSeconds;
            budget = maxBudget;
            lastTime = startTime;
        }

        public float Budget => budget;

        /// <summary>
        /// Returns true and spends budget when moving <paramref name="distance"/> at <paramref name="time"/> is allowed.
        /// A rejected move spends nothing, so the player can continue from the last accepted position.
        /// </summary>
        /// <param name="speedScale">How much faster than normal the character may move now (haste: above 1).</param>
        public bool TryMove(float distance, double time, float speedScale = 1f)
        {
            speedScale = Mathf.Max(1f, speedScale);
            float elapsed = Mathf.Max(0f, (float)(time - lastTime));
            lastTime = time;
            budget = Mathf.Min(maxBudget * speedScale, budget + elapsed * maxSpeed * speedScale);

            if (distance > budget)
                return false;

            budget -= distance;
            return true;
        }

        /// <summary>Restores a full budget, for example after the server teleports the player.</summary>
        public void Reset(double time)
        {
            budget = maxBudget;
            lastTime = time;
        }
    }
}
