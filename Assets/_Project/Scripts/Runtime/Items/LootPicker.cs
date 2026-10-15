using System.Collections.Generic;
using RPG.Controls;
using RPG.Dungeons;
using RPG.Player;
using RPG.UI;
using RPG.Vendors;
using RPG.World;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// Owner: clicking a loot bag, a portal, a vendor, a waystone or a chest walks to it and then picks the loot up,
    /// uses the portal, opens the vendor's or waystone's window, or opens the chest.
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
        private Waystone pendingWaystone;
        private LootChest pendingChest;
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
                pendingWaystone = pending == null && pendingPortal == null && pendingVendor == null ? UnderPointer<Waystone>() : null;
                pendingChest = pending == null && pendingPortal == null && pendingVendor == null && pendingWaystone == null
                    ? UnderPointer<LootChest>() : null;
                if (pendingChest != null && !pendingChest.IsSpawned) pendingChest = null;
                if (pending != null || pendingPortal != null || pendingVendor != null || pendingWaystone != null || pendingChest != null)
                    movement.SuppressClickMoveUntilRelease();
            }
            if (input.MoveInput.sqrMagnitude > 0.01f)
            {
                pending = null;
                pendingPortal = null;
                pendingVendor = null;
                pendingWaystone = null;
                pendingChest = null;
            }
            if (pendingChest != null)
            {
                UpdateChest();
                return;
            }
            if (pendingWaystone != null)
            {
                UpdateWaystone();
                return;
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

        private void UpdateChest()
        {
            if (pendingChest == null || !pendingChest.IsSpawned)
            {
                pendingChest = null;
                return;
            }

            Vector2 target = pendingChest.transform.position;
            if (Vector2.Distance(transform.position, target) > pendingChest.OpenRange * 0.8f)
            {
                movement.SetDestination(target);
                return;
            }

            if (movement.Destination.HasValue) movement.Stop();
            inventory.RequestOpenChest(pendingChest);
            pendingChest = null;
        }

        private void UpdateWaystone()
        {
            Vector2 target = pendingWaystone.transform.position;
            if (Vector2.Distance(transform.position, target) > pendingWaystone.UseRange * 0.8f)
            {
                movement.SetDestination(target);
                return;
            }

            if (movement.Destination.HasValue) movement.Stop();
            WaystoneWindow.Open(pendingWaystone);
            pendingWaystone = null;
        }

        private T UnderPointer<T>() where T : Component
        {
            if (worldCamera == null) worldCamera = Camera.main;
            if (worldCamera == null) return null;

            Vector2 point = worldCamera.ScreenToWorldPoint(input.PointerPosition);
            Physics2D.OverlapPoint(point, ContactFilter2D.noFilter, pointerHits);
            foreach (Collider2D hit in pointerHits)
            {
                T found = hit.GetComponentInParent<T>();
                if (found != null) return found;
            }
            return null;
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
