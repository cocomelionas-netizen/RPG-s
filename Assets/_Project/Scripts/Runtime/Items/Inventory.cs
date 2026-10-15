using System;
using System.Collections.Generic;
using RPG.Combat;
using RPG.Controls;
using RPG.Progression;
using RPG.UI;
using RPG.Vendors;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Items
{
    /// <summary>
    /// A player's bag and worn equipment. The server owns both lists; the owner only asks to equip,
    /// take off, use or pick up, and the server checks that the move is valid.
    /// Equipped items add damage, health and armor (applied by <see cref="RPG.Progression.CharacterProgress"/>).
    /// Also holds the character's gold, earned from monsters and spent or earned at vendors.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Inventory : NetworkBehaviour
    {
        public const int EquipmentSlots = 4;

        [SerializeField] private ItemCatalog catalog;
        [SerializeField] private AffixCatalog affixes;
        [SerializeField, Min(1)] private int bagSize = 20;
        [Tooltip("Given to every new character.")]
        [SerializeField] private ItemDefinition[] startingItems = Array.Empty<ItemDefinition>();
        [SerializeField, Min(0f)] private float potionCooldown = 5f;
        [Tooltip("How close the player must be to pick up loot (the server allows a little more).")]
        [SerializeField, Min(0.1f)] private float pickupRange = 1.2f;
        [SerializeField, Min(0)] private int startingGold = 20;

        private const float TradeRangeTolerance = 0.5f;
        private static readonly Color GoldColor = new Color(1f, 0.82f, 0.25f);

        private NetworkList<ItemStack> bag;
        private NetworkList<ItemStack> equipment;
        private readonly NetworkVariable<int> gold = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private Health health;
        private Mana mana;
        private CharacterProgress progress;
        private PlayerInputReader input;
        private double potionReadyAt;

        /// <summary>Raised on every peer when the bag or the equipment changed.</summary>
        public event Action Changed;

        public ItemCatalog Catalog => catalog;
        public AffixCatalog Affixes => affixes;
        public int BagSize => bag != null ? bag.Count : 0;
        public float PickupRange => pickupRange;
        public int Gold => gold.Value;

        /// <summary>Sum of the equipped items' bonuses.</summary>
        public int DamageBonus { get; private set; }
        public int HealthBonus { get; private set; }
        public int Armor { get; private set; }
        public int ManaBonus { get; private set; }
        public AttributeSet Attributes { get; private set; }

        private void Awake()
        {
            health = GetComponent<Health>();
            mana = GetComponent<Mana>();
            progress = GetComponent<CharacterProgress>();
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
                gold.Value = startingGold;
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

        /// <summary>The random affix of a stack (null when it has none).</summary>
        public AffixDefinition Affix(ItemStack stack) => affixes != null && !stack.IsEmpty ? affixes.Get(stack.AffixId) : null;

        /// <summary>The item's name with its affix, for example "Iron Sword of the Bear".</summary>
        public string DisplayName(ItemStack stack)
        {
            ItemDefinition item = Definition(stack);
            return item != null ? item.NameWith(Affix(stack)) : "";
        }

        public ItemRarity Quality(ItemStack stack)
        {
            ItemDefinition item = Definition(stack);
            return item != null ? item.QualityWith(Affix(stack)) : ItemRarity.Common;
        }

        /// <summary>How many of an item the bag holds.</summary>
        public int CountOf(ItemDefinition item)
        {
            int id = catalog != null ? catalog.IdOf(item) : 0;
            int count = 0;
            for (int i = 0; i < bag.Count; i++)
                if (bag[i].ItemId == id) count += bag[i].Count;
            return count;
        }

        /// <summary>The first bag slot holding an item used this way (-1 when there is none).</summary>
        public int FindUsable(ItemUse use)
        {
            for (int i = 0; i < bag.Count; i++)
            {
                ItemDefinition item = Definition(bag[i]);
                if (item != null && item.use == use) return i;
            }
            return -1;
        }

        /// <summary>The potion keys: 5 drinks a healing potion, 6 a mana potion.</summary>
        public static ItemUse PotionKeyUse(int index) => index == 0 ? ItemUse.Heal : ItemUse.RestoreMana;

        private void Update()
        {
            if (!IsSpawned || !IsOwner) return;
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (input == null || !input.enabled) return;
            for (int i = 0; i < PlayerInputReader.PotionKeyCount; i++)
            {
                if (!input.PotionPressed(i)) continue;
                int slot = FindUsable(PotionKeyUse(i));
                if (slot >= 0) RequestUse(slot);
                else MessageLog.Add(i == 0 ? "You have no healing potions." : "You have no mana potions.", MessageLog.WarningColor);
            }
        }

        private void OnListChanged(NetworkListEvent<ItemStack> change)
        {
            RecalculateBonuses();
            Changed?.Invoke();
        }

        private void RecalculateBonuses()
        {
            int damage = 0, healthBonus = 0, armor = 0, manaBonus = 0;
            AttributeSet attributes = default;
            for (int i = 0; i < equipment.Count; i++)
            {
                ItemDefinition item = Definition(equipment[i]);
                if (item == null) continue;
                damage += item.damageBonus;
                healthBonus += item.healthBonus;
                armor += item.armor;
                manaBonus += item.manaBonus;
                attributes += item.attributes;

                AffixDefinition affix = Affix(equipment[i]);
                if (affix == null) continue;
                damage += affix.damageBonus;
                healthBonus += affix.healthBonus;
                armor += affix.armor;
                manaBonus += affix.manaBonus;
                attributes += affix.attributes;
            }
            DamageBonus = damage;
            HealthBonus = healthBonus;
            Armor = armor;
            ManaBonus = manaBonus;
            Attributes = attributes;
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
                equipment[i] = item != null && (int)item.slot - 1 == i && IsKnown(stack) ? new ItemStack(stack.ItemId, 1, stack.AffixId) : ItemStack.Empty;
            }
        }

        private bool IsKnown(ItemStack stack)
        {
            ItemDefinition item = Definition(stack);
            bool affixKnown = stack.AffixId == 0 || Affix(stack) != null;
            return item != null && affixKnown && stack.Count <= item.maxStack;
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

        public void RequestBuy(Vendor vendor, int stockIndex)
        {
            if (IsOwner && vendor != null) BuyRpc(vendor.Index, stockIndex);
        }

        public void RequestSell(Vendor vendor, int bagIndex)
        {
            if (IsOwner && vendor != null) SellRpc(vendor.Index, bagIndex);
        }

        [Rpc(SendTo.Server)]
        private void BuyRpc(int vendorIndex, int stockIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerBuy(Vendor.Get(vendorIndex), stockIndex);
        }

        [Rpc(SendTo.Server)]
        private void SellRpc(int vendorIndex, int bagIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerSell(Vendor.Get(vendorIndex), bagIndex);
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
        private void MessageRpc(InventoryMessage message, int itemId, int affixId)
        {
            ItemDefinition item = catalog != null ? catalog.Get(itemId) : null;
            AffixDefinition affix = affixes != null ? affixes.Get(affixId) : null;
            Vector3 above = transform.position + Vector3.up * 1.1f;
            switch (message)
            {
                case InventoryMessage.Received:
                    if (item == null) break;
                    CombatTextLayer.ShowText(above, "+ " + item.NameWith(affix), ItemDefinition.RarityColor(item.QualityWith(affix)), 0.8f);
                    MessageLog.Add($"You receive [{item.NameWith(affix)}].", ItemDefinition.RarityColor(item.QualityWith(affix)));
                    break;
                case InventoryMessage.BagFull:
                    CombatTextLayer.ShowText(above, "Inventory is full", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add("Inventory is full.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.NotReady:
                    CombatTextLayer.ShowText(above, "Not ready yet", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add("Not ready yet.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.WrongClass:
                    CombatTextLayer.ShowText(above, "Your class cannot use that", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add("Your class cannot use that.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.NotEnoughGold:
                    CombatTextLayer.ShowText(above, "Not enough gold", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add("Not enough gold.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.Gold:
                    CombatTextLayer.ShowText(above, $"+{itemId} gold", GoldColor, 0.8f);
                    MessageLog.Add($"You receive {itemId} gold.", GoldColor);
                    break;
            }
        }

        // ---------- Server ----------

        /// <summary>Server: adds items (with an optional random affix id), filling stacks first. Returns how many fitted.</summary>
        public int ServerAddItem(ItemDefinition item, int count = 1, bool announce = true, int affixId = 0)
        {
            if (!IsServer || catalog == null || item == null || count <= 0) return 0;
            int id = catalog.IdOf(item);
            if (id == 0) return 0;
            if (affixes == null || affixes.Get(affixId) == null) affixId = 0;
            var kind = new ItemStack(id, 1, affixId);

            int remaining = count;
            for (int i = 0; i < bag.Count && remaining > 0; i++)
            {
                ItemStack stack = bag[i];
                if (stack.IsEmpty || !stack.SameKind(kind) || stack.Count >= item.maxStack) continue;
                int added = Mathf.Min(remaining, item.maxStack - stack.Count);
                bag[i] = stack.WithCount(stack.Count + added);
                remaining -= added;
            }
            for (int i = 0; i < bag.Count && remaining > 0; i++)
            {
                if (!bag[i].IsEmpty) continue;
                int added = Mathf.Min(remaining, item.maxStack);
                bag[i] = kind.WithCount(added);
                remaining -= added;
            }

            int fitted = count - remaining;
            if (announce && fitted > 0) MessageRpc(InventoryMessage.Received, id, affixId);
            if (remaining > 0) MessageRpc(InventoryMessage.BagFull, id, 0);
            return fitted;
        }

        /// <summary>Server: wears the item from a bag slot; whatever was worn there goes back into that bag slot.</summary>
        public bool ServerEquip(int bagIndex)
        {
            if (!IsServer || bagIndex < 0 || bagIndex >= bag.Count || health.IsDead) return false;
            ItemStack stack = bag[bagIndex];
            ItemDefinition item = Definition(stack);
            if (item == null || !item.IsEquippable) return false;
            if (progress != null && !item.CanBeUsedByClass(progress.ClassIndex))
            {
                MessageRpc(InventoryMessage.WrongClass, stack.ItemId, 0);
                return false;
            }

            int equipIndex = (int)item.slot - 1;
            ItemStack worn = equipment[equipIndex];
            equipment[equipIndex] = stack.WithCount(1);
            bag[bagIndex] = stack.Count > 1 ? stack.WithCount(stack.Count - 1) : worn;
            if (stack.Count > 1 && !worn.IsEmpty) ServerAddItem(Definition(worn), 1, false, worn.AffixId);
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
            MessageRpc(InventoryMessage.BagFull, worn.ItemId, 0);
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
                MessageRpc(InventoryMessage.NotReady, stack.ItemId, 0);
                return false;
            }

            switch (item.use)
            {
                case ItemUse.Heal:
                    health.Heal(Mathf.Max(1, Mathf.RoundToInt(health.Max * item.healFraction)), true);
                    break;
                case ItemUse.RestoreMana:
                    if (mana == null) return false;
                    mana.ServerRestore(Mathf.Max(1, Mathf.RoundToInt(mana.Max * item.healFraction)));
                    break;
            }
            potionReadyAt = now + potionCooldown;

            bag[bagIndex] = stack.Count > 1 ? stack.WithCount(stack.Count - 1) : ItemStack.Empty;
            return true;
        }

        /// <summary>Server: adds (or with a negative amount removes) gold. Shows gains to the owner.</summary>
        public void ServerAddGold(int amount, bool announce = true)
        {
            if (!IsServer || amount == 0) return;
            gold.Value = Mathf.Max(0, gold.Value + amount);
            if (announce && amount > 0) MessageRpc(InventoryMessage.Gold, amount, 0);
        }

        /// <summary>Server: restores saved gold.</summary>
        public void ServerSetGold(int amount)
        {
            if (IsServer) gold.Value = Mathf.Max(0, amount);
        }

        private bool IsNear(Vendor vendor)
        {
            return vendor != null && vendor.Definition != null
                   && Vector2.Distance(AutoAttack.ServerPosition(this), vendor.transform.position) <= vendor.TradeRange + TradeRangeTolerance;
        }

        /// <summary>Server: buys one item from a vendor the player stands next to.</summary>
        public bool ServerBuy(Vendor vendor, int stockIndex)
        {
            if (!IsServer || health.IsDead || !IsNear(vendor)) return false;
            ItemDefinition[] stock = vendor.Definition.stock;
            if (stockIndex < 0 || stockIndex >= stock.Length || stock[stockIndex] == null) return false;

            ItemDefinition item = stock[stockIndex];
            if (gold.Value < item.price)
            {
                MessageRpc(InventoryMessage.NotEnoughGold, 0, 0);
                return false;
            }
            if (ServerAddItem(item, 1) <= 0) return false;
            gold.Value -= item.price;
            return true;
        }

        /// <summary>Server: sells one item from a bag slot to a vendor the player stands next to.</summary>
        public bool ServerSell(Vendor vendor, int bagIndex)
        {
            if (!IsServer || health.IsDead || !IsNear(vendor) || bagIndex < 0 || bagIndex >= bag.Count) return false;
            ItemStack stack = bag[bagIndex];
            ItemDefinition item = Definition(stack);
            int price = item != null ? item.SellPriceWith(Affix(stack)) : 0;
            if (price <= 0) return false;

            bag[bagIndex] = stack.Count > 1 ? stack.WithCount(stack.Count - 1) : ItemStack.Empty;
            ServerAddGold(price);
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
            WrongClass,
            NotEnoughGold,
            /// <summary>Gold gained; the amount travels in the item id field.</summary>
            Gold,
        }
    }
}
