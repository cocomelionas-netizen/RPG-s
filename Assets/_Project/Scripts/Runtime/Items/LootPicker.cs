using System.Collections.Generic;
using RPG.Controls;
using RPG.Player;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// Owner: clicking a loot bag walks to it and picks it up when close enough. WASD or another click cancels.
    /// Runs before <see cref="PlayerMovement"/> so the click on the bag does not also become a move elsewhere.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    [RequireComponent(typeof(Inventory))]
    public class LootPicker : NetworkBehaviour
    {
        private Inventory inventory;
        private PlayerInputReader input;
        private PlayerMovement movement;
        private Camera worldCamera;
        private LootBag pending;
        private readonly List<Collider2D> pointerHits = new List<Collider2D>();

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            input = GetComponent<PlayerInputReader>();
            movement = GetComponent<PlayerMovement>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner) enabled = false;
        }

        private void Update()
        {
            if (!IsOwner || input == null || movement == null || !input.enabled) return;

            if (input.MoveClickPressed && !input.IsPointerOverUI)
            {
                pending = LootUnderPointer();
                if (pending != null) movement.SuppressClickMoveUntilRelease();
            }
            if (input.MoveInput.sqrMagnitude > 0.01f) pending = null;
            if (pending == null) return;
            if (!pending.IsSpawned)
            {
                pending = null;
                return;
            }

            Vector2 target = pending.transform.position;
            if (Vector2.Distance(transform.position, target) > inventory.PickupRange * 0.8f)
            {
                movement.SetDestination(target);
                return;
            }

            if (movement.Destination.HasValue) movement.Stop();
            inventory.RequestPickUp(pending);
            pending = null;
        }

        private LootBag LootUnderPointer()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return null;

            Vector2 point = worldCamera.ScreenToWorldPoint(input.PointerPosition);
            Physics2D.OverlapPoint(point, ContactFilter2D.noFilter, pointerHits);
            foreach (Collider2D hit in pointerHits)
            {
                LootBag loot = hit.GetComponentInParent<LootBag>();
                if (loot != null && loot.IsSpawned) return loot;
            }
            return null;
        }
    }
}
