using System;
using System.Collections.Generic;
using RPG.Combat;
using RPG.Controls;
using RPG.Progression;
using RPG.UI;
using RPG.Vendors;
using RPG.World;
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
        [Tooltip("Seconds between potions (shared by all of them); the source game: 60.")]
        [SerializeField, Min(0f)] private float potionCooldown = 60f;
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
        // Server time when potions can be used again (shared by all potions); replicated so the bar can show it.
        private readonly NetworkVariable<double> potionReadyAt = new NetworkVariable<double>(
            0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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

        /// <summary>The gem set in an item's socket (null when none).</summary>
        public ItemDefinition Gem(ItemStack stack) => stack.HasGem && catalog != null ? catalog.Get(stack.Socket) : null;

        /// <summary>"Socket: empty" or "Socket: Ruby (+4 Strength)"; null when the item has no socket.</summary>
        public string SocketText(ItemStack stack)
        {
            if (!stack.HasSocket) return null;
            ItemDefinition gem = Gem(stack);
            return gem == null ? "Socket: empty" : $"Socket: {gem.displayName} ({gem.attributes.Describe(", ")})";
        }

        /// <summary>Share Bartering takes off what vendors charge and adds to what they pay.</summary>
        public float BarteringBonus => progress != null ? AttributeRules.BarteringDiscount(progress.TotalAttributes) : 0f;

        /// <summary>What a vendor pays for one: the item with its affix, plus the gem in its socket, plus Bartering.</summary>
        public int SellPrice(ItemStack stack)
        {
            ItemDefinition item = Definition(stack);
            if (item == null) return 0;
            ItemDefinition gem = Gem(stack);
            int price = item.SellPriceWith(Affix(stack)) + (gem != null ? gem.SellPrice : 0);
            return Mathf.FloorToInt(price * (1f + BarteringBonus));
        }

        /// <summary>What a vendor charges for an item, less Bartering.</summary>
        public int BuyPrice(ItemDefinition item) => item != null ? Mathf.CeilToInt(item.price * (1f - BarteringBonus)) : 0;

        /// <summary>Tooltip text of a stack, with its affix, socket and real sell price.</summary>
        public string Describe(ItemStack stack, string className)
        {
            ItemDefinition item = Definition(stack);
            return item != null ? item.Describe(className, Affix(stack), SocketText(stack), SellPrice(stack)) : "";
        }

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

        /// <summary>
        /// Server: sets a gem from the bag into the chosen item: a worn one (<paramref name="intoEquipment"/>, index in
        /// slot order Weapon, Head, Chest, Feet) or one in the bag. Only works when that item has an empty socket.
        /// Gems stay there for good.
        /// </summary>
        public bool ServerSocketGem(int gemBagIndex, bool intoEquipment, int targetIndex)
        {
            if (!IsServer || gemBagIndex < 0 || gemBagIndex >= bag.Count || health.IsDead) return false;
            ItemStack gemStack = bag[gemBagIndex];
            ItemDefinition gem = Definition(gemStack);
            if (gem == null || !gem.isGem) return false;

            NetworkList<ItemStack> list = intoEquipment ? equipment : bag;
            if (targetIndex < 0 || targetIndex >= list.Count || (!intoEquipment && targetIndex == gemBagIndex)) return false;
            ItemStack target = list[targetIndex];
            if (target.IsEmpty) return false;
            if (target.Socket != ItemStack.EmptySocket)
            {
                MessageRpc(InventoryMessage.NoEmptySocket, target.ItemId, 0);
                return false;
            }

            list[targetIndex] = target.WithSocket(gemStack.ItemId);
            bag[gemBagIndex] = gemStack.Count > 1 ? gemStack.WithCount(gemStack.Count - 1) : ItemStack.Empty;
            // The item the gem went into travels in the affix field.
            MessageRpc(InventoryMessage.GemSocketed, gemStack.ItemId, target.ItemId);
            return true;
        }

        /// <summary>Server: at a jeweler, turns every three of the same gem into one better gem. Returns how many were made.</summary>
        public int ServerCombineGems(Vendor jeweler)
        {
            if (!IsServer || health.IsDead || !IsNear(jeweler) || !jeweler.Definition.combinesGems) return 0;
            int made = Combine(item => item.isGem);
            MessageRpc(made > 0 ? InventoryMessage.GemsCombined : InventoryMessage.NothingToCombine, made, 0);
            return made;
        }

        /// <summary>
        /// Server: at a vendor that does it, turns every five of the same potion into one of the next tier (the source game's
        /// recipes: 5 Minor Life Potions make a Lesser Life Potion, and so on). Returns how many were made.
        /// </summary>
        public int ServerCombinePotions(Vendor vendor)
        {
            if (!IsServer || health.IsDead || !IsNear(vendor) || !vendor.Definition.combinesPotions) return 0;
            int made = Combine(item => item.use != ItemUse.None);
            MessageRpc(made > 0 ? InventoryMessage.PotionsCombined : InventoryMessage.NothingToCombinePotions, made, 0);
            return made;
        }

        /// <summary>Turns every Combine Count of the chosen items into what they combine into; how many were made.</summary>
        private int Combine(Predicate<ItemDefinition> which)
        {
            int made = 0;
            foreach (ItemDefinition item in catalog.items)
            {
                if (item == null || item.combinesInto == null || !which(item)) continue;
                int count = item.combineCount;
                while (CountOf(item) >= count && ServerRemoveItem(item, count))
                {
                    if (ServerAddItem(item.combinesInto, 1, true) > 0)
                    {
                        made++;
                        continue;
                    }
                    // No room for the result: give them back and stop.
                    ServerAddItem(item, count, false);
                    break;
                }
            }
            return made;
        }

        /// <summary>Server: takes items out of the bag (keys, quest items). False, and nothing taken, when there are not enough.</summary>
        public bool ServerRemoveItem(ItemDefinition item, int count)
        {
            if (!IsServer || item == null || count <= 0 || CountOf(item) < count) return false;
            int id = catalog.IdOf(item);
            for (int i = bag.Count - 1; i >= 0 && count > 0; i--)
            {
                ItemStack stack = bag[i];
                if (stack.ItemId != id || stack.IsEmpty) continue;
                int taken = Mathf.Min(count, stack.Count);
                bag[i] = stack.Count > taken ? stack.WithCount(stack.Count - taken) : ItemStack.Empty;
                count -= taken;
            }
            return true;
        }

        /// <summary>Seconds until potions can be used again (0 when ready).</summary>
        public float PotionCooldownRemaining =>
            IsSpawned ? Mathf.Max(0f, (float)(potionReadyAt.Value - NetworkManager.ServerTime.Time)) : 0f;

        /// <summary>Share of the potion cooldown still to wait, 1 right after drinking.</summary>
        public float PotionCooldownFraction => potionCooldown > 0f ? Mathf.Clamp01(PotionCooldownRemaining / potionCooldown) : 0f;

        /// <summary>
        /// The bag slot of the best item used this way that the player's level allows: the highest tier, a serum
        /// before a potion of the same tier (-1 when there is none).
        /// </summary>
        public int FindUsable(ItemUse use)
        {
            int level = progress != null ? progress.Level : int.MaxValue;
            int best = -1;
            ItemDefinition bestItem = null;
            for (int i = 0; i < bag.Count; i++)
            {
                ItemDefinition item = Definition(bag[i]);
                if (item == null || item.use != use || !item.CanBeUsedAtLevel(level)) continue;
                if (bestItem == null || item.itemLevel > bestItem.itemLevel
                    || (item.itemLevel == bestItem.itemLevel && item.rarity > bestItem.rarity))
                {
                    best = i;
                    bestItem = item;
                }
            }
            return best;
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

                ItemDefinition gem = Gem(equipment[i]);
                if (gem != null)
                {
                    damage += gem.damageBonus;
                    healthBonus += gem.healthBonus;
                    armor += gem.armor;
                    manaBonus += gem.manaBonus;
                    attributes += gem.attributes;
                }

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
                equipment[i] = item != null && (int)item.slot - 1 == i && IsKnown(stack) ? new ItemStack(stack.ItemId, 1, stack.AffixId, stack.Socket) : ItemStack.Empty;
            }
        }

        private bool IsKnown(ItemStack stack)
        {
            ItemDefinition item = Definition(stack);
            bool affixKnown = stack.AffixId == 0 || Affix(stack) != null;
            ItemDefinition gem = Gem(stack);
            bool socketValid = !stack.HasSocket || (item != null && item.IsEquippable && (!stack.HasGem || (gem != null && gem.isGem)));
            return item != null && affixKnown && socketValid && stack.Count <= item.maxStack;
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

        /// <summary>Owner: opens a chest the player stands next to.</summary>
        public void RequestOpenChest(LootChest chest)
        {
            if (IsOwner && chest != null && chest.IsSpawned) OpenChestRpc(chest.NetworkObject);
        }

        [Rpc(SendTo.Server)]
        private void OpenChestRpc(NetworkObjectReference chestReference, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (chestReference.TryGet(out NetworkObject chestObject) && chestObject.TryGetComponent(out LootChest chest))
                chest.ServerOpen(health);
        }

        /// <summary>Owner: sets the gem in a bag slot into a worn item (<paramref name="intoEquipment"/>, index in slot order) or a bag item.</summary>
        public void RequestSocketGem(int gemBagIndex, bool intoEquipment, int targetIndex)
        {
            if (IsOwner) SocketGemRpc(gemBagIndex, intoEquipment, targetIndex);
        }

        public void RequestCombineGems(Vendor jeweler)
        {
            if (IsOwner && jeweler != null) CombineGemsRpc(jeweler.Index);
        }

        public void RequestCombinePotions(Vendor vendor)
        {
            if (IsOwner && vendor != null) CombinePotionsRpc(vendor.Index);
        }

        [Rpc(SendTo.Server)]
        private void CombinePotionsRpc(int vendorIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerCombinePotions(Vendor.Get(vendorIndex));
        }

        [Rpc(SendTo.Server)]
        private void SocketGemRpc(int gemBagIndex, bool intoEquipment, int targetIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerSocketGem(gemBagIndex, intoEquipment, targetIndex);
        }

        [Rpc(SendTo.Server)]
        private void CombineGemsRpc(int vendorIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerCombineGems(Vendor.Get(vendorIndex));
        }

        /// <summary>Owner: asks a vendor to craft a piece from its craft list out of materials in the bag.</summary>
        public void RequestCraft(Vendor vendor, int craftIndex)
        {
            if (IsOwner && vendor != null) CraftRpc(vendor.Index, craftIndex);
        }

        [Rpc(SendTo.Server)]
        private void CraftRpc(int vendorIndex, int craftIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerCraft(Vendor.Get(vendorIndex), craftIndex);
        }

        /// <summary>Owner: asks how many of each item a vendor has left (the answer updates <see cref="Vendor.KnownRemaining"/>).</summary>
        public void RequestVendorStock(Vendor vendor)
        {
            if (IsOwner && vendor != null) VendorStockRequestRpc(vendor.Index);
        }

        [Rpc(SendTo.Server)]
        private void VendorStockRequestRpc(int vendorIndex, RpcParams rpcParams = default)
        {
            Vendor vendor = Vendor.Get(vendorIndex);
            if (rpcParams.Receive.SenderClientId == OwnerClientId && vendor != null && vendor.Definition != null)
                VendorStockRpc(vendorIndex, vendor.ServerStockSnapshot());
        }

        [Rpc(SendTo.Owner)]
        private void VendorStockRpc(int vendorIndex, int[] counts)
        {
            Vendor vendor = Vendor.Get(vendorIndex);
            if (vendor != null) vendor.SetKnownStock(counts);
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

        /// <summary>Owner: shows what lies in a loot bag the player stands next to (the answer opens the loot window).</summary>
        public void RequestLootContents(LootBag loot)
        {
            if (IsOwner && loot != null && loot.IsSpawned) LootContentsRequestRpc(loot.NetworkObject);
        }

        /// <summary>Owner: takes one item out of a loot bag (<paramref name="index"/> as the loot window shows it).</summary>
        public void RequestTakeLoot(LootBag loot, int index, int itemId)
        {
            if (IsOwner && loot != null && loot.IsSpawned) TakeLootRpc(loot.NetworkObject, index, itemId);
        }

        /// <summary>
        /// Raised on the owner with what a loot bag holds (by its network id), after asking or taking from it; an
        /// empty list when it is gone or out of reach.
        /// </summary>
        public static event Action<ulong, ItemStack[]> LootContentsReceived;

        [Rpc(SendTo.Server)]
        private void LootContentsRequestRpc(NetworkObjectReference lootReference, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (lootReference.TryGet(out NetworkObject lootObject) && lootObject.TryGetComponent(out LootBag loot))
                SendLootContents(loot);
        }

        [Rpc(SendTo.Server)]
        private void TakeLootRpc(NetworkObjectReference lootReference, int index, int itemId, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
            if (!lootReference.TryGet(out NetworkObject lootObject) || !lootObject.TryGetComponent(out LootBag loot)) return;
            ulong id = loot.NetworkObjectId;
            if (InLootRange(loot)) loot.ServerGiveOne(this, index, itemId);
            if (loot.IsSpawned) SendLootContents(loot);
            else LootContentsRpc(id, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());
        }

        private bool InLootRange(LootBag loot) =>
            !health.IsDead && Vector2.Distance(AutoAttack.ServerPosition(this), loot.transform.position) <= pickupRange + 0.4f;

        /// <summary>Server: opens the owner's loot window on a bag (a chest they just opened).</summary>
        public void ServerShowLoot(LootBag loot)
        {
            if (IsServer && loot != null && loot.IsSpawned) SendLootContents(loot);
        }

        /// <summary>Server: tells the owner a lock is being picked, or that their Lockpicking is too low for it.</summary>
        public void ServerLockMessage(bool picking, int required, int have)
        {
            if (IsServer) MessageRpc(picking ? InventoryMessage.PickingLock : InventoryMessage.LockpickingTooLow, required, have);
        }

        /// <summary>Server: tells the owner what is in the bag (nothing when it is out of reach).</summary>
        private void SendLootContents(LootBag loot)
        {
            int count = InLootRange(loot) ? loot.Contents.Count : 0;
            var ids = new int[count];
            var counts = new int[count];
            var affixIds = new int[count];
            var sockets = new int[count];
            for (int i = 0; i < count; i++)
            {
                LootItem item = loot.Contents[i];
                ids[i] = catalog.IdOf(item.Item);
                counts[i] = item.Count;
                affixIds[i] = item.AffixId;
                sockets[i] = item.Socket;
            }
            LootContentsRpc(loot.NetworkObjectId, ids, counts, affixIds, sockets);
        }

        [Rpc(SendTo.Owner)]
        private void LootContentsRpc(ulong lootId, int[] ids, int[] counts, int[] affixIds, int[] sockets)
        {
            var stacks = new ItemStack[ids.Length];
            for (int i = 0; i < ids.Length; i++) stacks[i] = new ItemStack(ids[i], counts[i], affixIds[i], sockets[i]);
            LootContentsReceived?.Invoke(lootId, stacks);
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
            {
                ulong id = loot.NetworkObjectId;
                ServerPickUp(loot);
                // Keeps an open loot window up to date (or closes it when everything was taken).
                if (loot.IsSpawned) SendLootContents(loot);
                else LootContentsRpc(id, Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>(), Array.Empty<int>());
            }
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
                case InventoryMessage.GemSocketed:
                {
                    if (item == null) break;
                    ItemDefinition target = catalog != null ? catalog.Get(affixId) : null;
                    CombatTextLayer.ShowText(above, $"{item.displayName} set", ItemDefinition.RarityColor(item.rarity), 0.8f);
                    MessageLog.Add($"You set [{item.displayName}] into [{(target != null ? target.displayName : "your gear")}].",
                        ItemDefinition.RarityColor(item.rarity));
                    break;
                }
                case InventoryMessage.NoEmptySocket:
                    CombatTextLayer.ShowText(above, "No empty socket there", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add(item != null ? $"[{item.displayName}] has no empty socket." : "That item has no empty socket.",
                        MessageLog.WarningColor);
                    break;
                case InventoryMessage.GemsCombined:
                    MessageLog.Add($"The jeweler made {itemId} better gem(s) for you.", GoldColor);
                    break;
                case InventoryMessage.NothingToCombine:
                    MessageLog.Add("You need 3 of the same gem to combine them.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.PotionsCombined:
                    MessageLog.Add($"You combined your potions into {itemId} stronger one(s).", GoldColor);
                    break;
                case InventoryMessage.NothingToCombinePotions:
                    MessageLog.Add("You need 5 of the same potion to combine them.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.LevelTooLow:
                    CombatTextLayer.ShowText(above, "Your level is too low", new Color(1f, 0.4f, 0.3f), 0.8f);
                    if (item != null)
                        MessageLog.Add($"You need level {item.itemLevel} to {(item.IsEquippable ? "wear" : "use")} [{item.displayName}].", MessageLog.WarningColor);
                    break;
                case InventoryMessage.SoldOut:
                    CombatTextLayer.ShowText(above, "Sold out", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add("That item is sold out. Vendors restock every few minutes.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.Crafted:
                    if (item == null) break;
                    CombatTextLayer.ShowText(above, "Crafted " + item.NameWith(affix), ItemDefinition.RarityColor(item.QualityWith(affix)), 0.8f);
                    MessageLog.Add($"You craft [{item.NameWith(affix)}].", ItemDefinition.RarityColor(item.QualityWith(affix)));
                    break;
                case InventoryMessage.NotEnoughMaterials:
                    CombatTextLayer.ShowText(above, "Not enough materials", new Color(1f, 0.4f, 0.3f), 0.8f);
                    // The count travels in the affix field.
                    if (item != null) MessageLog.Add($"You need {affixId} [{item.displayName}].", MessageLog.WarningColor);
                    break;
                case InventoryMessage.LearnedSpell:
                    if (item == null || item.teachesSpell == null) break;
                    CombatTextLayer.ShowText(above, $"Learned {item.teachesSpell.displayName}", new Color(0.75f, 0.55f, 1f), 1f);
                    MessageLog.Add($"You learned a new ability: {item.teachesSpell.displayName}. Open the spellbook (P) to place it on your bar.",
                        new Color(0.75f, 0.55f, 1f));
                    break;
                case InventoryMessage.AlreadyKnown:
                    MessageLog.Add("You already know that ability.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.PickingLock:
                    CombatTextLayer.ShowText(above, "Picking the lock...", new Color(0.85f, 0.8f, 0.6f), 0.8f);
                    MessageLog.Add("You pick the lock...", new Color(0.85f, 0.8f, 0.6f));
                    break;
                case InventoryMessage.LockpickingTooLow:
                    // Needed in the item id field, what the player has in the affix field.
                    CombatTextLayer.ShowText(above, "Lockpicking not high enough", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add($"Lockpicking not high enough: this lock needs {itemId}, you have {affixId}. " +
                                   "Raise Lockpicking in the character sheet (C, Combat page).", MessageLog.WarningColor);
                    break;
                case InventoryMessage.Gold:
                    CombatTextLayer.ShowText(above, $"+{itemId} gold", GoldColor, 0.8f);
                    MessageLog.Add($"You receive {itemId} gold.", GoldColor);
                    break;
            }
        }

        // ---------- Server ----------

        /// <summary>Server: adds items (with an optional random affix id), filling stacks first. Returns how many fitted.</summary>
        public int ServerAddItem(ItemDefinition item, int count = 1, bool announce = true, int affixId = 0, int socket = 0)
        {
            if (!IsServer || catalog == null || item == null || count <= 0) return 0;
            int id = catalog.IdOf(item);
            if (id == 0) return 0;
            if (affixes == null || affixes.Get(affixId) == null) affixId = 0;
            ItemDefinition gem = socket > 0 ? catalog.Get(socket) : null;
            if (!item.IsEquippable || (socket > 0 && (gem == null || !gem.isGem))) socket = item.IsEquippable && socket != 0 ? ItemStack.EmptySocket : 0;
            var kind = new ItemStack(id, 1, affixId, socket);

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
            if (progress != null && !item.CanBeWornAtLevel(progress.Level))
            {
                MessageRpc(InventoryMessage.LevelTooLow, stack.ItemId, 0);
                return false;
            }

            int equipIndex = (int)item.slot - 1;
            ItemStack worn = equipment[equipIndex];
            equipment[equipIndex] = stack.WithCount(1);
            bag[bagIndex] = stack.Count > 1 ? stack.WithCount(stack.Count - 1) : worn;
            if (stack.Count > 1 && !worn.IsEmpty) ServerAddItem(Definition(worn), 1, false, worn.AffixId, worn.Socket);
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
            if (item.use == ItemUse.LearnSpell) return ServerLearnFrom(bagIndex, stack, item);
            if (item.use == ItemUse.Combine) return ServerCombineFrom(item);
            if (item.use == ItemUse.RestoreMana && mana == null) return false;
            if (progress != null && !item.CanBeUsedAtLevel(progress.Level))
            {
                MessageRpc(InventoryMessage.LevelTooLow, stack.ItemId, 0);
                return false;
            }

            double now = NetworkManager.ServerTime.Time;
            if (now < potionReadyAt.Value)
            {
                MessageRpc(InventoryMessage.NotReady, stack.ItemId, 0);
                return false;
            }

            // A serum restores it all at once; a potion a share now and the rest every 2 seconds.
            int total = item.RestoreTotal(item.use == ItemUse.Heal ? health.Max : mana.Max);
            int ticks = item.RestoreTicks;
            int perTick = Mathf.Max(1, Mathf.CeilToInt(total / (float)ticks));
            Restore(item.use, perTick);
            if (ticks > 1) StartCoroutine(RestoreOverTime(item.use, perTick, ticks - 1));
            potionReadyAt.Value = now + potionCooldown;

            bag[bagIndex] = stack.Count > 1 ? stack.WithCount(stack.Count - 1) : ItemStack.Empty;
            return true;
        }

        /// <summary>
        /// Server: puts an item together with the ones it combines with (one of each) into its result, like the source game's
        /// Dale Gem on a Depleted Key. Nothing is used up when one is missing or there is no room.
        /// </summary>
        public bool ServerCombineFrom(ItemDefinition item)
        {
            if (!IsServer || item == null || item.combineResult == null) return false;
            foreach (ItemDefinition other in item.combineWith)
            {
                if (other == null || CountOf(other) >= (other == item ? 2 : 1)) continue;
                MessageRpc(InventoryMessage.NotEnoughMaterials, catalog.IdOf(other), 1);
                return false;
            }
            ServerRemoveItem(item, 1);
            foreach (ItemDefinition other in item.combineWith)
                if (other != null) ServerRemoveItem(other, 1);
            if (ServerAddItem(item.combineResult, 1, false) <= 0)
            {
                // No room: give everything back.
                ServerAddItem(item, 1, false);
                foreach (ItemDefinition other in item.combineWith)
                    if (other != null) ServerAddItem(other, 1, false);
                return false;
            }
            MessageRpc(InventoryMessage.Crafted, catalog.IdOf(item.combineResult), 0);
            return true;
        }

        /// <summary>Server: reads a Tome: its class and level, then the spell is learned and the Tome used up.</summary>
        private bool ServerLearnFrom(int bagIndex, ItemStack stack, ItemDefinition tome)
        {
            var spellbook = GetComponent<Spellbook>();
            if (spellbook == null || tome.teachesSpell == null) return false;
            if (progress != null && !tome.CanBeUsedByClass(progress.ClassIndex))
            {
                MessageRpc(InventoryMessage.WrongClass, stack.ItemId, 0);
                return false;
            }
            if (progress != null && !tome.CanBeUsedAtLevel(progress.Level))
            {
                MessageRpc(InventoryMessage.LevelTooLow, stack.ItemId, 0);
                return false;
            }
            if (!spellbook.ServerLearn(tome.teachesSpell))
            {
                MessageRpc(InventoryMessage.AlreadyKnown, stack.ItemId, 0);
                return false;
            }
            bag[bagIndex] = stack.Count > 1 ? stack.WithCount(stack.Count - 1) : ItemStack.Empty;
            MessageRpc(InventoryMessage.LearnedSpell, stack.ItemId, 0);
            return true;
        }

        private void Restore(ItemUse use, int amount)
        {
            if (use == ItemUse.Heal) health.Heal(amount, true);
            else if (mana != null) mana.ServerRestore(amount);
        }

        private System.Collections.IEnumerator RestoreOverTime(ItemUse use, int perTick, int ticks)
        {
            var wait = new WaitForSeconds(ItemDefinition.RestoreInterval);
            for (int i = 0; i < ticks; i++)
            {
                yield return wait;
                // Dying ends it, like any effect.
                if (!IsSpawned || health.IsDead) yield break;
                Restore(use, perTick);
            }
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
            if (vendor.ServerRemaining(stockIndex) == 0)
            {
                MessageRpc(InventoryMessage.SoldOut, 0, 0);
                VendorStockRpc(vendor.Index, vendor.ServerStockSnapshot());
                return false;
            }
            int price = BuyPrice(item);
            if (gold.Value < price)
            {
                MessageRpc(InventoryMessage.NotEnoughGold, 0, 0);
                return false;
            }
            if (ServerAddItem(item, 1) <= 0) return false;
            gold.Value -= price;
            vendor.ServerTake(stockIndex);
            VendorStockRpc(vendor.Index, vendor.ServerStockSnapshot());
            return true;
        }

        /// <summary>
        /// Server: a vendor the player stands next to crafts a piece from its craft list: it takes the materials and
        /// gives the piece, which may come out better (<see cref="LootDrop.CraftChances"/>) and with a socket.
        /// The player must be of the piece's level.
        /// </summary>
        public bool ServerCraft(Vendor vendor, int craftIndex)
        {
            if (!IsServer || health.IsDead || !IsNear(vendor)) return false;
            ItemDefinition[] crafts = vendor.Definition.crafts;
            if (crafts == null || craftIndex < 0 || craftIndex >= crafts.Length) return false;
            ItemDefinition item = crafts[craftIndex];
            if (item == null || !item.IsCraftable) return false;
            if (progress != null && progress.Level < item.itemLevel)
            {
                MessageRpc(InventoryMessage.LevelTooLow, catalog.IdOf(item), 0);
                return false;
            }
            if (CountOf(item.craftMaterial) < item.craftCount)
            {
                MessageRpc(InventoryMessage.NotEnoughMaterials, catalog.IdOf(item.craftMaterial), item.craftCount);
                return false;
            }

            int level = progress != null ? progress.Level : item.itemLevel;
            ItemRarity? better = LootDrop.RollQuality(LootDrop.CraftChances);
            int affix = 0;
            if (affixes != null)
                affix = better.HasValue && better.Value > item.rarity ? affixes.RollQuality(item, level, better.Value) : affixes.Roll(item, level);
            int socket = UnityEngine.Random.value < CraftSocketChance ? ItemStack.EmptySocket : 0;

            ServerRemoveItem(item.craftMaterial, item.craftCount);
            if (ServerAddItem(item, 1, false, affix, socket) <= 0)
            {
                // No room: the materials go back.
                ServerAddItem(item.craftMaterial, item.craftCount, false);
                return false;
            }
            MessageRpc(InventoryMessage.Crafted, catalog.IdOf(item), affix);
            return true;
        }

        /// <summary>Chance that a crafted piece has an empty socket.</summary>
        public const float CraftSocketChance = 0.2f;

        /// <summary>Server: sells one item from a bag slot to a vendor the player stands next to.</summary>
        public bool ServerSell(Vendor vendor, int bagIndex)
        {
            if (!IsServer || health.IsDead || !IsNear(vendor) || bagIndex < 0 || bagIndex >= bag.Count) return false;
            ItemStack stack = bag[bagIndex];
            ItemDefinition item = Definition(stack);
            int price = item != null ? SellPrice(stack) : 0;
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
            SoldOut,
            /// <summary>A gem was set; the gem travels in the item id field, the item it went into in the affix field.</summary>
            GemSocketed,
            /// <summary>The chosen item has no empty socket; it travels in the item id field.</summary>
            NoEmptySocket,
            /// <summary>Gems combined; how many new gems travels in the item id field.</summary>
            GemsCombined,
            NothingToCombine,
            /// <summary>The player's level is below the item's; the item travels in the item id field.</summary>
            LevelTooLow,
            /// <summary>A piece was crafted: the item and its affix.</summary>
            Crafted,
            /// <summary>Too few materials: the material travels in the item id field, how many are needed in the affix field.</summary>
            NotEnoughMaterials,
            /// <summary>Potions combined; how many new potions travels in the item id field.</summary>
            PotionsCombined,
            NothingToCombinePotions,
            /// <summary>Picking a lock: the Lockpicking needed travels in the item id field, the player's in the affix field.</summary>
            PickingLock,
            LockpickingTooLow,
            /// <summary>A Tome was read; it travels in the item id field.</summary>
            LearnedSpell,
            AlreadyKnown,
        }
    }
}
