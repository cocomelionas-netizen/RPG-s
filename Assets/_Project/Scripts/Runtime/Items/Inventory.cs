using System;
using System.Collections.Generic;
using RPG.Combat;
using RPG.UI;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// A player's bag and worn equipment. The server owns both lists; the owner only asks to equip,
    /// take off, use or pick up, and the server checks that the move is valid.
    /// Equipped items add damage, health and armor (applied by <see cref="RPG.Progression.CharacterProgress"/>).
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Inventory : NetworkBehaviour
    {
        public const int EquipmentSlots = 4;

        [SerializeField] private ItemCatalog catalog;
        [SerializeField, Min(1)] private int bagSize = 20;
        [Tooltip("Given to every new character.")]
        [SerializeField] private ItemDefinition[] startingItems = Array.Empty<ItemDefinition>();
        [SerializeField, Min(0f)] private float potionCooldown = 5f;
        [Tooltip("How close the player must be to pick up loot (the server allows a little more).")]
        [SerializeField, Min(0.1f)] private float pickupRange = 1.2f;

        private NetworkList<ItemStack> bag;
        private NetworkList<ItemStack> equipment;
        private Health health;
        private double potionReadyAt;

        /// <summary>Raised on every peer when the bag or the equipment changed.</summary>
        public event Action Changed;

        public ItemCatalog Catalog => catalog;
        public int BagSize => bag != null ? bag.Count : 0;
        public float PickupRange => pickupRange;

        /// <summary>Sum of the equipped items' bonuses.</summary>
        public int DamageBonus { get; private set; }
        public int HealthBonus { get; private set; }
        public int Armor { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            bag = new NetworkList<ItemStack>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
            equipment = new NetworkList<ItemStack>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        }

        public override void OnNetworkSpawn()
        {
            bag.OnListChanged += OnListChanged;
            equipment.OnListChanged += OnListChanged;

            if (IsServer)
            {
                bag.Clear();
                equipment.Clear();
                for (int i = 0; i < bagSize; i++) bag.Add(ItemStack.Empty);
                for (int i = 0; i < EquipmentSlots; i++) equipment.Add(ItemStack.Empty);
                foreach (ItemDefinition item in startingItems)
                    ServerAddItem(item, 1, false);
            }
            RecalculateBonuses();
        }

        public override void OnNetworkDespawn()
        {
            bag.OnListChanged -= OnListChanged;
            equipment.OnListChanged -= OnListChanged;
        }

        public ItemStack GetBagSlot(int index) => index >= 0 && index < bag.Count ? bag[index] : ItemStack.Empty;

        public ItemStack GetEquipped(ItemSlot slot)
        {
            int index = (int)slot - 1;
            return index >= 0 && index < equipment.Count ? equipment[index] : ItemStack.Empty;
        }

        public ItemDefinition Definition(ItemStack stack) => catalog != null && !stack.IsEmpty ? catalog.Get(stack.ItemId) : null;

        /// <summary>How many of an item the bag holds.</summary>
        public int CountOf(ItemDefinition item)
        {
            int id = catalog != null ? catalog.IdOf(item) : 0;
            int count = 0;
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].ItemId == id) count += bag[i].Count;
            return count;
        }

        private void OnListChanged(NetworkListEvent<ItemStack> change)
        {
            RecalculateBonuses();
            Changed?.Invoke();
        }

        private void RecalculateBonuses()
        {
            int damage = 0, healthBonus = 0, armor = 0;
            for (int i = 0; i < equipment.Count; i++)
            {
                ItemDefinition item = Definition(equipment[i]);
                if (item == null) continue;
                damage += item.damageBonus;
                healthBonus += item.healthBonus;
                armor += item.armor;
            }
            DamageBonus = damage;
            HealthBonus = healthBonus;
            Armor = armor;
        }

        /// <summary>Server: replaces the bag and equipment with saved ones (unknown items are dropped).</summary>
        public void ServerLoad(IReadOnlyList<ItemStack> savedBag, IReadOnlyList<ItemStack> savedEquipment)
        {
            if (!IsServer) return;
            for (int i = 0; i < bag.Count; i++)
                bag[i] = savedBag != null && i < savedBag.Count && IsKnown(savedBag[i]) ? savedBag[i] : ItemStack.Empty;
            for (int i = 0; i < equipment.Count; i++)
            {
                ItemStack stack = savedEquipment != null && i < savedEquipment.Count ? savedEquipment[i] : ItemStack.Empty;
                ItemDefinition item = Definition(stack);
                // Only something that belongs in this slot.
                equipment[i] = item != null && (int)item.slot - 1 == i ? new ItemStack(stack.ItemId, 1) : ItemStack.Empty;
            }
        }

        private bool IsKnown(ItemStack stack)
        {
            ItemDefinition item = Definition(stack);
            return item != null && stack.Count <= item.maxStack;
        }

        /// <summary>The bag's contents, slot by slot.</summary>
        public ItemStack[] BagSnapshot()
        {
            var copy = new ItemStack[bag.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = bag[i];
            return copy;
        }

        /// <summary>The worn items, in slot order (Weapon, Head, Chest, Feet).</summary>
        public ItemStack[] EquipmentSnapshot()
        {
            var copy = new ItemStack[equipment.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = equipment[i];
            return copy;
        }

        // ---------- Owner ----------

        public void RequestEquip(int bagIndex) { if (IsOwner) EquipRpc(bagIndex); }
        public void RequestUnequip(ItemSlot slot) { if (IsOwner) UnequipRpc(slot); }
        public void RequestUse(int bagIndex) { if (IsOwner) UseRpc(bagIndex); }

        /// <summary>Owner: equips an equippable item or uses a usable one.</summary>
        public void RequestActivate(int bagIndex)
        {
            ItemDefinition item = Definition(GetBagSlot(bagIndex));
            if (item == null) return;
            if (item.IsEquippable) RequestEquip(bagIndex);
            else if (item.use != ItemUse.None) RequestUse(bagIndex);
        }

        public void RequestPickUp(LootBag loot)
        {
            if (IsOwner && loot != null && loot.IsSpawned) PickUpRpc(loot.NetworkObject);
        }

        [Rpc(SendTo.Server)]
        private void EquipRpc(int bagIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerEquip(bagIndex);
        }

        [Rpc(SendTo.Server)]
        private void UnequipRpc(ItemSlot slot, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerUnequip(slot);
        }

        [Rpc(SendTo.Server)]
        private void UseRpc(int bagIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerUse(bagIndex);
        }

        [Rpc(SendTo.Server)]
        private void PickUpRpc(NetworkObjectReference lootReference, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (lootReference.TryGet(out NetworkObject lootObject) && lootObject.TryGetComponent(out LootBag loot))
                ServerPickUp(loot);
        }

        [Rpc(SendTo.Owner)]
        private void MessageRpc(InventoryMessage message, int itemId)
        {
            ItemDefinition item = catalog != null ? catalog.Get(itemId) : null;
            Vector3 above = transform.position + Vector3.up * 1.1f;
            switch (message)
            {
                case InventoryMessage.Received:
                    if (item != null) CombatTextLayer.ShowText(above, "+ " + item.displayName, ItemDefinition.RarityColor(item.rarity), 0.8f);
                    break;
                case InventoryMessage.BagFull:
                    CombatTextLayer.ShowText(above, "Inventory is full", new Color(1f, 0.4f, 0.3f), 0.8f);
                    break;
                case InventoryMessage.NotReady:
                    CombatTextLayer.ShowText(above, "Not ready yet", new Color(1f, 0.4f, 0.3f), 0.8f);
                    break;
            }
        }

        // ---------- Server ----------

        /// <summary>Server: adds items, filling stacks first. Returns how many fitted.</summary>
        public int ServerAddItem(ItemDefinition item, int count = 1, bool announce = true)
        {
            if (!IsServer || catalog == null || item == null || count <= 0) return 0;
            int id = catalog.IdOf(item);
            if (id == 0) return 0;

            int remaining = count;
            for (int i = 0; i < bag.Count && remaining > 0; i++)
            {
                ItemStack stack = bag[i];
                if (stack.ItemId != id || stack.Count >= item.maxStack) continue;
                int added = Mathf.Min(remaining, item.maxStack - stack.Count);
                bag[i] = new ItemStack(id, stack.Count + added);
                remaining -= added;
            }
            for (int i = 0; i < bag.Count && remaining > 0; i++)
            {
                if (!bag[i].IsEmpty) continue;
                int added = Mathf.Min(remaining, item.maxStack);
                bag[i] = new ItemStack(id, added);
                remaining -= added;
            }

            int fitted = count - remaining;
            if (announce && fitted > 0) MessageRpc(InventoryMessage.Received, id);
            if (remaining > 0) MessageRpc(InventoryMessage.BagFull, id);
            return fitted;
        }

        /// <summary>Server: wears the item from a bag slot; whatever was worn there goes back into that bag slot.</summary>
        public bool ServerEquip(int bagIndex)
        {
            if (!IsServer || bagIndex < 0 || bagIndex >= bag.Count || health.IsDead) return false;
            ItemStack stack = bag[bagIndex];
            ItemDefinition item = Definition(stack);
            if (item == null || !item.IsEquippable) return false;

            int equipIndex = (int)item.slot - 1;
            ItemStack worn = equipment[equipIndex];
            equipment[equipIndex] = new ItemStack(stack.ItemId, 1);
            bag[bagIndex] = stack.Count > 1 ? new ItemStack(stack.ItemId, stack.Count - 1) : worn;
            if (stack.Count > 1 && !worn.IsEmpty) ServerAddItem(Definition(worn), 1, false);
            return true;
        }

        /// <summary>Server: takes off the item in a slot and puts it in the bag (fails when the bag is full).</summary>
        public bool ServerUnequip(ItemSlot slot)
        {
            if (!IsServer || slot == ItemSlot.None || health.IsDead) return false;
            int equipIndex = (int)slot - 1;
            ItemStack worn = equipment[equipIndex];
            if (worn.IsEmpty) return false;

            for (int i = 0; i < bag.Count; i++)
            {
                if (!bag[i].IsEmpty) continue;
                bag[i] = worn;
                equipment[equipIndex] = ItemStack.Empty;
                return true;
            }
            MessageRpc(InventoryMessage.BagFull, worn.ItemId);
            return false;
        }

        /// <summary>Server: uses one item from a bag slot (potions).</summary>
        public bool ServerUse(int bagIndex)
        {
            if (!IsServer || bagIndex < 0 || bagIndex >= bag.Count || health.IsDead) return false;
            ItemStack stack = bag[bagIndex];
            ItemDefinition item = Definition(stack);
            if (item == null || item.use == ItemUse.None) return false;

            double now = NetworkManager.ServerTime.Time;
            if (now < potionReadyAt)
            {
                MessageRpc(InventoryMessage.NotReady, stack.ItemId);
                return false;
            }

            switch (item.use)
            {
                case ItemUse.Heal:
                    health.Heal(Mathf.Max(1, Mathf.RoundToInt(health.Max * item.healFraction)), true);
                    potionReadyAt = now + potionCooldown;
                    break;
            }

            bag[bagIndex] = stack.Count > 1 ? new ItemStack(stack.ItemId, stack.Count - 1) : ItemStack.Empty;
            return true;
        }

        /// <summary>Server: takes what fits from a loot bag the player stands next to.</summary>
        public bool ServerPickUp(LootBag loot)
        {
            if (!IsServer || loot == null || !loot.IsSpawned || health.IsDead) return false;
            float distance = Vector2.Distance(AutoAttack.ServerPosition(this), loot.transform.position);
            if (distance > pickupRange + 0.4f) return false;
            return loot.ServerGiveTo(this);
        }

        public enum InventoryMessage : byte
        {
            Received,
            BagFull,
            NotReady,
        }
    }
}
