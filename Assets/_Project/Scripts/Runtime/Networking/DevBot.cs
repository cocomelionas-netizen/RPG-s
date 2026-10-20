using System;
using RPG.Player;
using Unity.Netcode;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RPG.Networking
{
    /// <summary>
    /// Test bot: with <c>-bot</c> on the command line, the local player wanders around its spawn point
    /// using normal click-to-move, so multiplayer can be tested alone (or load-tested with many clients).
    /// Does nothing without the argument.
    /// </summary>
    public class DevBot : MonoBehaviour
    {
        public const string Argument = "-bot";

        [SerializeField, Min(0.1f)] private float wanderRadius = 2.5f;
        [SerializeField] private Vector2 pauseSeconds = new Vector2(0.5f, 2f);

        private PlayerMovement movement;
        private Vector2 home;
        private float nextMoveTime;

        private void Awake()
        {
            bool requested = Array.Exists(Environment.GetCommandLineArgs(),
                arg => string.Equals(arg, Argument, StringComparison.OrdinalIgnoreCase));
            enabled = requested;
        }

        private void Update()
        {
            if (movement == null)
            {
                NetworkManager network = NetworkManager.Singleton;
                NetworkObject player = network != null && network.LocalClient != null ? network.LocalClient.PlayerObject : null;
                if (player == null) return;

                movement = player.GetComponent<PlayerMovement>();
                home = player.transform.position;
            }

            // Dead: presses Respawn like a player would.
            if (movement.TryGetComponent(out RPG.Combat.Health health) && health.IsDead)
            {
                if (Time.time >= nextMoveTime && movement.TryGetComponent(out RPG.Combat.PlayerRespawn respawn)) respawn.RequestRespawn();
                nextMoveTime = Time.time + 2f;
                return;
            }
            if (movement.Destination.HasValue || Time.time < nextMoveTime) return;

            Vector2 offset = Random.insideUnitCircle * wanderRadius;
            movement.SetDestination(home + new Vector2(offset.x, offset.y * 0.5f));
            nextMoveTime = Time.time + Random.Range(pauseSeconds.x, pauseSeconds.y);
        }
    }
}
