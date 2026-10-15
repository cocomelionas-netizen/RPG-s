using System.Collections.Generic;
using RPG.Controls;
using RPG.Dungeons;
using RPG.Player;
using RPG.UI;
using RPG.Vendors;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// Owner: clicking a loot bag, a portal or a vendor walks to it and then picks the loot up, uses the portal
    /// or opens the vendor's window.
    /// WASD or another click cancels.
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
        private Portal pendingPortal;
        private Vendor pendingVendor;
        private DungeonTraveller traveller;
        private readonly List<Collider2D> pointerHits = new List<Collider2D>();

        private void Awake()
        {
            inventory = GetComponent<Inventory>();
            input = GetComponent<PlayerInputReader>();
            movement = GetComponent<PlayerMovement>();
            traveller = GetComponent<DungeonTraveller>();
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
                pendingPortal = pending == null && traveller != null ? PortalUnderPointer() : null;
                pendingVendor = pending == null && pendingPortal == null ? VendorUnderPointer() : null;
                if (pending != null || pendingPortal != null || pendingVendor != null) movement.SuppressClickMoveUntilRelease();
            }
            if (input.MoveInput.sqrMagnitude > 0.01f)
            {
                pending = null;
                pendingPortal = null;
                pendingVendor = null;
            }
            if (pendingVendor != null)
            {
                UpdateVendor();
                return;
            }
            if (pendingPortal != null)
            {
                UpdatePortal();
                return;
            }
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

        private void UpdatePortal()
        {
            if (pendingPortal == null || !pendingPortal.isActiveAndEnabled)
            {
                pendingPortal = null;
                return;
            }

            DungeonManager manager = DungeonManager.Instance;
            float range = manager != null ? manager.PortalRange : 1.2f;
            Vector2 target = pendingPortal.transform.position;
            if (Vector2.Distance(transform.position, target) > range * 0.7f)
            {
                movement.SetDestination(target);
                return;
            }

            if (movement.Destination.HasValue) movement.Stop();
            traveller.RequestUsePortal();
            pendingPortal = null;
        }

        private void UpdateVendor()
        {
            Vector2 target = pendingVendor.transform.position;
            if (Vector2.Distance(transform.position, target) > pendingVendor.TradeRange * 0.8f)
            {
                movement.SetDestination(target);
                return;
            }

            if (movement.Destination.HasValue) movement.Stop();
            VendorWindow.Open(pendingVendor);
            pendingVendor = null;
        }

        private Vendor VendorUnderPointer()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return null;

            Vector2 point = worldCamera.ScreenToWorldPoint(input.PointerPosition);
            Physics2D.OverlapPoint(point, ContactFilter2D.noFilter, pointerHits);
            foreach (Collider2D hit in pointerHits)
            {
                Vendor vendor = hit.GetComponentInParent<Vendor>();
                if (vendor != null) return vendor;
            }
            return null;
        }

        private Portal PortalUnderPointer()
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return null;

            Vector2 point = worldCamera.ScreenToWorldPoint(input.PointerPosition);
            Physics2D.OverlapPoint(point, ContactFilter2D.noFilter, pointerHits);
            foreach (Collider2D hit in pointerHits)
            {
                Portal portal = hit.GetComponentInParent<Portal>();
                if (portal != null) return portal;
            }
            return null;
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
