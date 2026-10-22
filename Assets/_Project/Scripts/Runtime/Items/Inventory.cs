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
        /// <summary>
        /// The source game's equipment: Weapon, Helm, Chest, Feet, Necklace, Belt, Legs, Hands, two Rings and the Offhand
        /// (shield). Index order: <see cref="SlotAt"/> (the first four keep their old places, so saves stay).
        /// </summary>
        public const int EquipmentSlots = 11;

        private static readonly ItemSlot[] SlotOrder =
        {
            ItemSlot.Weapon, ItemSlot.Head, ItemSlot.Chest, ItemSlot.Feet, ItemSlot.Neck, ItemSlot.Waist, ItemSlot.Legs,
            ItemSlot.Hands, ItemSlot.Ring, ItemSlot.Ring, ItemSlot.Shield,
        };

        /// <summary>What kind of item an equipment index holds.</summary>
        public static ItemSlot SlotAt(int index) => index >= 0 && index < SlotOrder.Length ? SlotOrder[index] : ItemSlot.None;

        /// <summary>The (first) equipment index of a slot; -1 for none.</summary>
        public static int IndexOf(ItemSlot slot) => slot == ItemSlot.None ? -1 : Array.IndexOf(SlotOrder, slot);

        [SerializeField] private ItemCatalog catalog;
        [SerializeField] private AffixCatalog affixes;
        [SerializeField, Min(1)] private int bagSize = 20;
        [Tooltip("Given to every new character.")]
        [SerializeField] private ItemDefinition[] startingItems = Array.Empty<ItemDefinition>();
        [Tooltip("Worn by a new character of a class, like the source game's starting gear: its plain weapon (and the warrior's shield).")]
        [SerializeField] private ClassGear[] startingGear = Array.Empty<ClassGear>();
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
        // Server time when potions can be used again (shared by all potions); replicated so the bar can show it.
        private readonly NetworkVariable<double> potionReadyAt = new NetworkVariable<double>(
            0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Scrolls of Returning: server time when another can be read, and the cast being read (end 0: none).
        private readonly NetworkVariable<double> returnReadyAt = new NetworkVariable<double>(
            0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> returnCastStart = new NetworkVariable<double>(
            0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<double> returnCastEnd = new NetworkVariable<double>(
            0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private const float ReturnMoveTolerance = 0.2f;
        // Food: server time when the next can be eaten; not within this many seconds of taking damage.
        private readonly NetworkVariable<double> foodReadyAt = new NetworkVariable<double>(
            0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public const float FoodCombatSeconds = 5f;

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

        /// <summary>A piece a new character of a class starts wearing.</summary>
        [Serializable]
        public struct ClassGear
        {
            public int classIndex;
            public ItemDefinition item;
        }

        /// <summary>The pieces new characters start wearing, by class.</summary>
        public ClassGear[] StartingGear => startingGear;

        /// <summary>Server: a new character puts on its class's starting gear (only into empty slots).</summary>
        public void ServerEquipStartingGear(int classIndex)
        {
            if (!IsServer || catalog == null) return;
            foreach (ClassGear gear in startingGear)
            {
                if (gear.classIndex != classIndex || gear.item == null || !gear.item.IsEquippable) continue;
                int index = IndexOf(gear.item.slot);
                int id = catalog.IdOf(gear.item);
                if (index < 0 || index >= equipment.Count || id <= 0 || !equipment[index].IsEmpty) continue;
                equipment[index] = new ItemStack(id, 1, 0);
            }
        }

        public ItemStack GetBagSlot(int index) => index >= 0 && index < bag.Count ? bag[index] : ItemStack.Empty;

        /// <summary>The item worn in a slot (for rings: the first ring).</summary>
        public ItemStack GetEquipped(ItemSlot slot) => GetEquippedAt(IndexOf(slot));

        /// <summary>The item worn at an equipment index (see <see cref="SlotAt"/>).</summary>
        public ItemStack GetEquippedAt(int index) => index >= 0 && index < equipment.Count ? equipment[index] : ItemStack.Empty;

        public ItemDefinition Definition(ItemStack stack) => catalog != null && !stack.IsEmpty ? catalog.Get(stack.ItemId) : null;

        /// <summary>The random affix of a stack (null when it has none).</summary>
        public AffixDefinition Affix(ItemStack stack) => affixes != null && !stack.IsEmpty ? affixes.Get(stack.AffixId) : null;

        /// <summary>The first gem set in an item (null when none).</summary>
        public ItemDefinition Gem(ItemStack stack)
        {
            for (int i = 0; i < ItemStack.MaxSockets; i++)
                if (GemAt(stack, i) is ItemDefinition gem) return gem;
            return null;
        }

        /// <summary>The gem in socket <paramref name="index"/> (null when it is empty or missing).</summary>
        public ItemDefinition GemAt(ItemStack stack, int index)
        {
            int socket = stack.SocketAt(index);
            return socket > 0 && catalog != null ? catalog.Get(socket) : null;
        }

        /// <summary>"Socket: empty" / "Socket: Ruby (+4 Strength)", a line per socket; null when the item has none.</summary>
        public string SocketText(ItemStack stack)
        {
            if (!stack.HasSocket) return null;
            var lines = new List<string>();
            for (int i = 0; i < ItemStack.MaxSockets; i++)
            {
                if (stack.SocketAt(i) == 0) continue;
                ItemDefinition gem = GemAt(stack, i);
                lines.Add(gem == null ? "Socket: empty" : $"Socket: {gem.displayName} ({gem.attributes.Describe(", ")})");
            }
            return string.Join("\n", lines);
        }

        /// <summary>Share Bartering takes off what vendors charge and adds to what they pay.</summary>
        public float BarteringBonus => progress != null ? AttributeRules.BarteringDiscount(progress.TotalAttributes) : 0f;

        /// <summary>What a vendor pays for one: the item with its affix, plus the gem in its socket, plus Bartering.</summary>
        public int SellPrice(ItemStack stack)
        {
            ItemDefinition item = Definition(stack);
            if (item == null) return 0;
            int price = item.SellPriceWith(Affix(stack));
            for (int i = 0; i < ItemStack.MaxSockets; i++)
                if (GemAt(stack, i) is ItemDefinition gem) price += gem.SellPrice;
            return Mathf.FloorToInt(price * (1f + BarteringBonus));
        }

        /// <summary>What a vendor charges for an item, less Bartering.</summary>
        public int BuyPrice(ItemDefinition item) => BuyPrice(item, null);

        /// <summary>What a vendor asks for an item with a random affix (better affixes cost more), after Bartering.</summary>
        public int BuyPrice(ItemDefinition item, AffixDefinition affix)
        {
            if (item == null) return 0;
            float price = item.price * (affix != null ? Mathf.Max(1f, affix.priceMultiplier) : 1f);
            return Mathf.CeilToInt(price * (1f - BarteringBonus));
        }

        /// <summary>Tooltip text of a stack, with its affix, socket and real sell price.</summary>
        public string Describe(ItemStack stack, string className)
        {
            ItemDefinition item = Definition(stack);
            if (item == null) return "";
            string text = item.Describe(className, Affix(stack), SocketText(stack), SellPrice(stack));
            if (!item.IsEquippable || (stack.EmpowerSteps == 0 && stack.EmpowerSpent == 0)) return text;
            int points = ItemDefinition.EmpowerPoints(Quality(stack));
            return text + $"\n<color=#c8a0ff>Empowered +{stack.EmpowerSteps} (item level {item.itemLevel + stack.EmpowerSteps}), "
                        + $"{Mathf.Max(0, points - stack.EmpowerSpent)} of {points} empowerment points left</color>";
        }

        /// <summary>The item's name with its affix, for example "Iron Sword of the Bear".</summary>
        public string DisplayName(ItemStack stack)
        {
            ItemDefinition item = Definition(stack);
            if (item == null) return "";
            return stack.EmpowerSteps > 0 ? $"{item.NameWith(Affix(stack))} +{stack.EmpowerSteps}" : item.NameWith(Affix(stack));
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
        /// equipment index order, see <see cref="SlotAt"/>) or one in the bag, into its first empty socket. Only works when
        /// that item has one.
        /// Gems stay there for good.
        /// </summary>
        public bool ServerSocketGem(int gemBagIndex, bool intoEquipment, int targetIndex)
        {
            if (!IsServer || gemBagIndex < 0 || gemBagIndex >= bag.Count || health.IsDead) return false;
            ItemStack gemStack = bag[gemBagIndex];
            ItemDefinition gem = Definition(gemStack);
            if (gem != null && gem.use == ItemUse.Empower) return ServerEmpower(gemBagIndex, intoEquipment, targetIndex);
            if (gem == null || !gem.isGem) return false;

            NetworkList<ItemStack> list = intoEquipment ? equipment : bag;
            if (targetIndex < 0 || targetIndex >= list.Count || (!intoEquipment && targetIndex == gemBagIndex)) return false;
            ItemStack target = list[targetIndex];
            if (target.IsEmpty) return false;
            int socket = target.FirstEmptySocket;
            if (socket < 0)
            {
                MessageRpc(InventoryMessage.NoEmptySocket, target.ItemId, 0);
                return false;
            }

            list[targetIndex] = target.WithSocketAt(socket, gemStack.ItemId);
            bag[gemBagIndex] = gemStack.Count > 1 ? gemStack.WithCount(gemStack.Count - 1) : ItemStack.Empty;
            // The item the gem went into travels in the affix field.
            MessageRpc(InventoryMessage.GemSocketed, gemStack.ItemId, target.ItemId);
            return true;
        }

        /// <summary>
        /// Server: uses an orb from the bag on a worn item or one in the bag. It must be gear of the orb's kind with an
        /// empowerment point left (its quality's number: <see cref="ItemDefinition.EmpowerPoints"/>); the orb is used up
        /// either way. Success (<see cref="ItemDefinition.EmpowerChance"/>) adds a step: +1 item level and
        /// <see cref="ItemDefinition.EmpowerShare"/> more damage or armor (accessories: +1 to each main attribute); failure
        /// only uses up the point. Arcane orbs always work, need no point, and add up to
        /// <see cref="ItemDefinition.EmpowerBeyondSteps"/> steps beyond.
        /// </summary>
        public bool ServerEmpower(int orbBagIndex, bool intoEquipment, int targetIndex)
        {
            if (!IsServer || orbBagIndex < 0 || orbBagIndex >= bag.Count || health.IsDead) return false;
            ItemStack orbStack = bag[orbBagIndex];
            ItemDefinition orb = Definition(orbStack);
            if (orb == null || orb.use != ItemUse.Empower) return false;
            NetworkList<ItemStack> list = intoEquipment ? equipment : bag;
            if (targetIndex < 0 || targetIndex >= list.Count || (!intoEquipment && targetIndex == orbBagIndex)) return false;
            ItemStack target = list[targetIndex];
            ItemDefinition gear = Definition(target);
            if (gear == null || !gear.IsEquippable || !orb.CanEmpower(gear.slot))
            {
                MessageRpc(InventoryMessage.CannotEmpower, orbStack.ItemId, 0);
                return false;
            }
            int points = ItemDefinition.EmpowerPoints(Quality(target));
            int steps = target.EmpowerSteps;
            int spent = target.EmpowerSpent;
            bool success;
            if (orb.empowerBeyond)
            {
                if (spent < points || steps >= points + ItemDefinition.EmpowerBeyondSteps)
                {
                    MessageRpc(InventoryMessage.NoEmpowerPoints, target.ItemId, 1);
                    return false;
                }
                success = true;
                steps++;
            }
            else
            {
                if (spent >= points)
                {
                    MessageRpc(InventoryMessage.NoEmpowerPoints, target.ItemId, 0);
                    return false;
                }
                success = UnityEngine.Random.value < ItemDefinition.EmpowerChance(orb.empowerPower);
                if (success) steps++;
                spent++;
            }
            list[targetIndex] = target.WithEmpower(steps, spent);
            bag[orbBagIndex] = orbStack.Count > 1 ? orbStack.WithCount(orbStack.Count - 1) : ItemStack.Empty;
            // The empowered item travels in the item field, its new step count in the affix field.
            MessageRpc(success ? InventoryMessage.Empowered : InventoryMessage.EmpowerFailed, target.ItemId, steps);
            return true;
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

        private double ServerNow => IsSpawned ? NetworkManager.ServerTime.Time : 0.0;

        /// <summary>Seconds until a Scroll of Returning can be read again (0 when ready).</summary>
        public float ReturnCooldownRemaining => IsSpawned ? Mathf.Max(0f, (float)(returnReadyAt.Value - ServerNow)) : 0f;

        /// <summary>Share of the scroll cooldown still to wait (1 right after reading one), for a cooldown of <paramref name="cooldown"/>.</summary>
        public float ReturnCooldownFraction(float cooldown) => cooldown > 0f ? Mathf.Clamp01(ReturnCooldownRemaining / cooldown) : 0f;

        /// <summary>Seconds until food can be eaten again (0 when ready).</summary>
        public float FoodCooldownRemaining => IsSpawned ? Mathf.Max(0f, (float)(foodReadyAt.Value - ServerNow)) : 0f;

        /// <summary>Whether a Scroll of Returning is being read.</summary>
        public bool IsReturning => IsSpawned && returnCastEnd.Value > 0.0;

        /// <summary>0..1 of the scroll being read (0 when none).</summary>
        public float ReturnProgress
        {
            get
            {
                if (!IsReturning || returnCastEnd.Value <= returnCastStart.Value) return 0f;
                return Mathf.Clamp01((float)((ServerNow - returnCastStart.Value) / (returnCastEnd.Value - returnCastStart.Value)));
            }
        }

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

        /// <summary>
        /// The bag slot an action bar item uses: that item when the bag has it; for a potion that ran out, the best potion
        /// of the same kind the player can drink (-1 when there is none).
        /// </summary>
        public int BagSlotForBarItem(ItemDefinition item)
        {
            if (item == null) return -1;
            int id = catalog != null ? catalog.IdOf(item) : 0;
            for (int i = 0; i < bag.Count; i++)
                if (!bag[i].IsEmpty && bag[i].ItemId == id) return i;
            return item.use == ItemUse.Heal || item.use == ItemUse.RestoreMana ? FindUsable(item.use) : -1;
        }

        /// <summary>How many of an action bar item the bag holds (counting the potions of its kind it would fall back to).</summary>
        public int CountForBarItem(ItemDefinition item)
        {
            int slot = BagSlotForBarItem(item);
            return slot >= 0 ? CountOf(Definition(bag[slot])) : 0;
        }

        /// <summary>Owner: uses an item from the action bar (by catalog id).</summary>
        public void RequestUseItem(int itemId)
        {
            ItemDefinition item = catalog != null ? catalog.Get(itemId) : null;
            if (!IsOwner || item == null) return;
            int slot = BagSlotForBarItem(item);
            if (slot >= 0) RequestUse(slot);
            else MessageLog.Add($"You have no {item.displayName}.", MessageLog.WarningColor);
        }

        private void OnListChanged(NetworkListEvent<ItemStack> change)
        {
            RecalculateBonuses();
            Changed?.Invoke();
        }

        /// <summary>What a piece adds when worn: its own stats, orb empowerment, its gems and its affix.</summary>
        public struct GearBonus
        {
            public int Damage;
            public int Health;
            public int Armor;
            public int Mana;
            public AttributeSet Attributes;
        }

        /// <summary>Everything an item stack would add worn (nothing for items that are not gear).</summary>
        public GearBonus BonusOf(ItemStack stack)
        {
            var bonus = new GearBonus();
            ItemDefinition item = Definition(stack);
            if (item == null || !item.IsEquippable) return bonus;
            bonus.Damage += item.damageBonus;
            bonus.Health += item.healthBonus;
            bonus.Armor += item.armor;
            bonus.Mana += item.manaBonus;
            bonus.Attributes += item.attributes;

            // Orb empowerment: a share more of the item's own damage and armor a step (at least 1), or attributes.
            int steps = stack.EmpowerSteps;
            if (steps > 0)
            {
                if (item.damageBonus > 0) bonus.Damage += Mathf.Max(steps, Mathf.RoundToInt(item.damageBonus * ItemDefinition.EmpowerShare * steps));
                if (item.armor > 0) bonus.Armor += Mathf.Max(steps, Mathf.RoundToInt(item.armor * ItemDefinition.EmpowerShare * steps));
                if (item.slot == ItemSlot.Neck || item.slot == ItemSlot.Waist || item.slot == ItemSlot.Ring)
                {
                    AttributeSet boost = default;
                    boost.strength = boost.agility = boost.intelligence = boost.vitality = boost.willpower = steps;
                    bonus.Attributes += boost;
                }
            }

            for (int socket = 0; socket < ItemStack.MaxSockets; socket++)
            {
                ItemDefinition gem = GemAt(stack, socket);
                if (gem == null) continue;
                bonus.Damage += gem.damageBonus;
                bonus.Health += gem.healthBonus;
                bonus.Armor += gem.armor;
                bonus.Mana += gem.manaBonus;
                bonus.Attributes += gem.attributes;
            }

            AffixDefinition affix = Affix(stack);
            if (affix == null) return bonus;
            bonus.Damage += affix.damageBonus;
            bonus.Health += affix.healthBonus;
            bonus.Armor += affix.armor;
            bonus.Mana += affix.manaBonus;
            bonus.Attributes += affix.attributes;
            return bonus;
        }

        private void RecalculateBonuses()
        {
            int damage = 0, healthBonus = 0, armor = 0, manaBonus = 0;
            AttributeSet attributes = default;
            for (int i = 0; i < equipment.Count; i++)
            {
                GearBonus bonus = BonusOf(equipment[i]);
                damage += bonus.Damage;
                healthBonus += bonus.Health;
                armor += bonus.Armor;
                manaBonus += bonus.Mana;
                attributes += bonus.Attributes;
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
                equipment[i] = item != null && item.slot == SlotAt(i) && IsKnown(stack) ? stack.WithCount(1) : ItemStack.Empty;
            }
        }

        private bool IsKnown(ItemStack stack)
        {
            ItemDefinition item = Definition(stack);
            bool affixKnown = stack.AffixId == 0 || Affix(stack) != null;
            bool socketValid = !stack.HasSocket || (item != null && item.IsEquippable);
            for (int i = 0; i < ItemStack.MaxSockets && socketValid; i++)
                if (stack.SocketAt(i) > 0 && (GemAt(stack, i) == null || !GemAt(stack, i).isGem)) socketValid = false;
            return item != null && affixKnown && socketValid && stack.Count <= item.maxStack;
        }

        /// <summary>The bag's contents, slot by slot.</summary>
        public ItemStack[] BagSnapshot()
        {
            var copy = new ItemStack[bag.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = bag[i];
            return copy;
        }

        /// <summary>The worn items, in equipment index order (<see cref="SlotAt"/>).</summary>
        public ItemStack[] EquipmentSnapshot()
        {
            var copy = new ItemStack[equipment.Count];
            for (int i = 0; i < copy.Length; i++) copy[i] = equipment[i];
            return copy;
        }

        // ---------- Owner ----------

        public void RequestEquip(int bagIndex) { if (IsOwner) EquipRpc(bagIndex); }
        public void RequestUnequip(ItemSlot slot) => RequestUnequipAt(IndexOf(slot));
        public void RequestUnequipAt(int equipIndex) { if (IsOwner) UnequipRpc(equipIndex); }
        public void RequestUse(int bagIndex) { if (IsOwner) UseRpc(bagIndex); }

        /// <summary>Owner: equips an equippable item or uses a usable one.</summary>
        public void RequestActivate(int bagIndex)
        {
            ItemDefinition item = Definition(GetBagSlot(bagIndex));
            if (item == null) return;
            if (item.IsEquippable) RequestEquip(bagIndex);
            else if (item.use != ItemUse.None) RequestUse(bagIndex);
        }

        /// <param name="expectedAffix">The quality the player saw (the vendor's known affix id); -1 buys whatever is there.</param>
        public void RequestBuy(Vendor vendor, int stockIndex, int expectedAffix = -1)
        {
            if (IsOwner && vendor != null) BuyRpc(vendor.Index, stockIndex, expectedAffix);
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

        [Rpc(SendTo.Server)]
        private void SocketGemRpc(int gemBagIndex, bool intoEquipment, int targetIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerSocketGem(gemBagIndex, intoEquipment, targetIndex);
        }

        /// <summary>Owner: asks a transmuter to turn what is in these bag slots into a recipe's result.</summary>
        public void RequestTransmute(Vendor vendor, int[] bagSlots)
        {
            if (IsOwner && vendor != null && bagSlots != null) TransmuteRpc(vendor.Index, bagSlots);
        }

        [Rpc(SendTo.Server)]
        private void TransmuteRpc(int vendorIndex, int[] bagSlots, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId && bagSlots.Length <= TransmuteSlots)
                ServerTransmute(Vendor.Get(vendorIndex), bagSlots);
        }

        public void RequestVendorStock(Vendor vendor)
        {
            if (IsOwner && vendor != null) VendorStockRequestRpc(vendor.Index);
        }

        [Rpc(SendTo.Server)]
        private void VendorStockRequestRpc(int vendorIndex, RpcParams rpcParams = default)
        {
            Vendor vendor = Vendor.Get(vendorIndex);
            if (rpcParams.Receive.SenderClientId == OwnerClientId && vendor != null && vendor.Definition != null)
                VendorStockRpc(vendorIndex, vendor.ServerStockSnapshot(), vendor.ServerAffixSnapshot());
        }

        [Rpc(SendTo.Owner)]
        private void VendorStockRpc(int vendorIndex, int[] counts, int[] affixIds)
        {
            Vendor vendor = Vendor.Get(vendorIndex);
            if (vendor != null) vendor.SetKnownStock(counts, affixIds);
        }

        public void RequestSell(Vendor vendor, int bagIndex)
        {
            if (IsOwner && vendor != null) SellRpc(vendor.Index, bagIndex);
        }

        [Rpc(SendTo.Server)]
        private void BuyRpc(int vendorIndex, int stockIndex, int expectedAffix, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerBuy(Vendor.Get(vendorIndex), stockIndex, expectedAffix);
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
                sockets[i] = item.Sockets;
            }
            LootContentsRpc(loot.NetworkObjectId, ids, counts, affixIds, sockets);
        }

        [Rpc(SendTo.Owner)]
        private void LootContentsRpc(ulong lootId, int[] ids, int[] counts, int[] affixIds, int[] sockets)
        {
            var stacks = new ItemStack[ids.Length];
            for (int i = 0; i < ids.Length; i++) stacks[i] = ItemStack.WithEmptySockets(ids[i], counts[i], affixIds[i], sockets[i]);
            LootContentsReceived?.Invoke(lootId, stacks);
        }

        [Rpc(SendTo.Server)]
        private void EquipRpc(int bagIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerEquip(bagIndex);
        }

        [Rpc(SendTo.Server)]
        private void UnequipRpc(int equipIndex, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerUnequipAt(equipIndex);
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
                case InventoryMessage.Empowered:
                    if (item == null) break;
                    CombatTextLayer.ShowText(above, $"Empowered +{affixId}", new Color(0.8f, 0.6f, 1f), 0.9f);
                    MessageLog.Add($"You successfully empowered [{item.displayName}] (+{affixId}).", new Color(0.8f, 0.6f, 1f));
                    break;
                case InventoryMessage.EmpowerFailed:
                    CombatTextLayer.ShowText(above, "Empowering failed", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add($"Your attempt to empower [{(item != null ? item.displayName : "the item")}] was unsuccessful: an empowerment point is used up.",
                        MessageLog.WarningColor);
                    break;
                case InventoryMessage.CannotEmpower:
                    CombatTextLayer.ShowText(above, "Wrong kind of item", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add(item != null ? $"[{item.displayName}] cannot empower that item." : "That orb cannot empower that item.",
                        MessageLog.WarningColor);
                    break;
                case InventoryMessage.NoEmpowerPoints:
                    CombatTextLayer.ShowText(above, "Cannot be empowered further", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add(affixId == 1
                        ? "An Arcane orb works only on gear whose empowerment points are all used, a few times."
                        : $"[{(item != null ? item.displayName : "That item")}] has no empowerment points left.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.NoEmptySocket:
                    CombatTextLayer.ShowText(above, "No empty socket there", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add(item != null ? $"[{item.displayName}] has no empty socket." : "That item has no empty socket.",
                        MessageLog.WarningColor);
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
                    CombatTextLayer.ShowText(above, "+ " + item.NameWith(affix), ItemDefinition.RarityColor(item.QualityWith(affix)), 0.8f);
                    MessageLog.Add($"You create [{item.NameWith(affix)}].", ItemDefinition.RarityColor(item.QualityWith(affix)));
                    break;
                case InventoryMessage.NoRecipe:
                    CombatTextLayer.ShowText(above, "Nothing to transmute", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add("Those items make nothing together.", MessageLog.WarningColor);
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
                case InventoryMessage.Returning:
                    MessageLog.Add($"You read the {(item != null ? item.displayName : "scroll")}... stand still.", new Color(0.55f, 0.85f, 1f));
                    break;
                case InventoryMessage.ReturnInterrupted:
                    CombatTextLayer.ShowText(above, "Interrupted", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add("Interrupted.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.InCombat:
                    CombatTextLayer.ShowText(above, "Cannot be used in combat", new Color(1f, 0.4f, 0.3f), 0.8f);
                    MessageLog.Add("You cannot use that in combat.", MessageLog.WarningColor);
                    break;
                case InventoryMessage.Returned:
                    MessageLog.Add($"You return to {Waystone.MainTownName}.", new Color(0.55f, 0.85f, 1f));
                    break;
                case InventoryMessage.Gold:
                    CombatTextLayer.ShowText(above, $"+{itemId} gold", GoldColor, 0.8f);
                    MessageLog.Add($"You receive {itemId} gold.", GoldColor);
                    break;
            }
        }

        // ---------- Server ----------

        /// <summary>
        /// Server: adds items (with an optional random affix id and a first socket: <see cref="ItemStack.EmptySocket"/> or a
        /// gem), filling stacks first. Returns how many fitted.
        /// </summary>
        public int ServerAddItem(ItemDefinition item, int count = 1, bool announce = true, int affixId = 0, int socket = 0)
        {
            if (!IsServer || catalog == null || item == null) return 0;
            return ServerAddStack(new ItemStack(catalog.IdOf(item), 1, affixId, socket), count, announce);
        }

        /// <summary>Server: adds gear with <paramref name="sockets"/> empty sockets (or anything else without).</summary>
        public int ServerAddItemWithSockets(ItemDefinition item, int count, bool announce, int affixId, int sockets)
        {
            if (!IsServer || catalog == null || item == null) return 0;
            return ServerAddStack(ItemStack.WithEmptySockets(catalog.IdOf(item), 1, affixId, sockets), count, announce);
        }

        /// <summary>Server: adds <paramref name="count"/> of an item like <paramref name="kind"/> (its affix and sockets). Returns how many fitted.</summary>
        public int ServerAddStack(ItemStack kind, int count, bool announce)
        {
            ItemDefinition item = Definition(kind.WithCount(1));
            if (!IsServer || catalog == null || item == null || count <= 0) return 0;
            int id = kind.ItemId;
            int affixId = affixes != null && affixes.Get(kind.AffixId) != null ? kind.AffixId : 0;
            // Sockets only on gear, and only gems in them.
            var cleaned = new ItemStack(id, 1, affixId);
            for (int i = 0, kept = 0; i < ItemStack.MaxSockets && item.IsEquippable; i++)
            {
                int socket = kind.SocketAt(i);
                if (socket == 0) continue;
                ItemDefinition gem = socket > 0 ? catalog.Get(socket) : null;
                cleaned = cleaned.WithSocketAt(kept++, socket > 0 && (gem == null || !gem.isGem) ? ItemStack.EmptySocket : socket);
            }
            kind = cleaned;

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

            int equipIndex = IndexOf(item.slot);
            // A second ring goes on the other hand when that one is free.
            if (item.slot == ItemSlot.Ring && !equipment[equipIndex].IsEmpty && equipment[equipIndex + 1].IsEmpty) equipIndex++;
            ItemStack worn = equipment[equipIndex];
            equipment[equipIndex] = stack.WithCount(1);
            bag[bagIndex] = stack.Count > 1 ? stack.WithCount(stack.Count - 1) : worn;
            if (stack.Count > 1 && !worn.IsEmpty) ServerAddStack(worn, 1, false);
            return true;
        }

        /// <summary>Server: takes off the item in a slot (for rings: the first) and puts it in the bag.</summary>
        public bool ServerUnequip(ItemSlot slot) => ServerUnequipAt(IndexOf(slot));

        /// <summary>Server: takes off the item at an equipment index and puts it in the bag (fails when the bag is full).</summary>
        public bool ServerUnequipAt(int equipIndex)
        {
            if (!IsServer || equipIndex < 0 || equipIndex >= equipment.Count || health.IsDead) return false;
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
            if (item.use == ItemUse.Return) return ServerStartReturn(item);
            if (item.use == ItemUse.Food) return ServerEat(bagIndex, stack, item);
            if (item.use == ItemUse.Buff) return ServerReadBuff(bagIndex, stack, item);
            // Orbs are used on an item (ServerEmpower), not on their own.
            if (item.use == ItemUse.Empower) return false;
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
        /// Server: starts reading a Scroll of Returning: after its cast time, if the player stood still and lives, they are
        /// taken to the main town and the scroll is used up. False when it is cooling down or one is already being read.
        /// </summary>
        public bool ServerStartReturn(ItemDefinition scroll)
        {
            if (!IsServer || scroll == null || scroll.use != ItemUse.Return || health.IsDead || CountOf(scroll) <= 0) return false;
            double now = ServerNow;
            if (IsReturning || now < returnReadyAt.Value)
            {
                MessageRpc(InventoryMessage.NotReady, catalog.IdOf(scroll), 0);
                return false;
            }
            if (Waystone.MainTown == null) return false;
            returnCastStart.Value = now;
            returnCastEnd.Value = now + Mathf.Max(0.01f, scroll.castSeconds);
            MessageRpc(InventoryMessage.Returning, catalog.IdOf(scroll), 0);
            StartCoroutine(ReadReturnScroll(scroll, AutoAttack.ServerPosition(health)));
            return true;
        }

        /// <summary>Server: eats food: health and mana back at once, not in combat, its own cooldown.</summary>
        private bool ServerEat(int bagIndex, ItemStack stack, ItemDefinition food)
        {
            double now = ServerNow;
            if (Time.time - health.LastDamageTime < FoodCombatSeconds)
            {
                MessageRpc(InventoryMessage.InCombat, stack.ItemId, 0);
                return false;
            }
            if (now < foodReadyAt.Value)
            {
                MessageRpc(InventoryMessage.NotReady, stack.ItemId, 0);
                return false;
            }
            health.Heal(food.restoreAmount, true);
            if (mana != null) mana.ServerRestore(food.restoreAmount);
            foodReadyAt.Value = now + food.useCooldown;
            bag[bagIndex] = stack.Count > 1 ? stack.WithCount(stack.Count - 1) : ItemStack.Empty;
            return true;
        }

        /// <summary>The status effect an attribute scroll gives (Intelligence: the Intellect buff).</summary>
        public static StatusKind BuffKindOf(RPG.Progression.Attribute attribute)
        {
            switch (attribute)
            {
                case RPG.Progression.Attribute.Strength: return StatusKind.StrengthBonus;
                case RPG.Progression.Attribute.Agility: return StatusKind.AgilityBonus;
                case RPG.Progression.Attribute.Intelligence: return StatusKind.Intellect;
                case RPG.Progression.Attribute.Willpower: return StatusKind.WillpowerBonus;
                default: return StatusKind.CourageBonus;
            }
        }

        /// <summary>
        /// Server: reads an attribute scroll: its attribute up for its time, not in combat and not while a scroll of the
        /// same attribute still works (the source game's cooldown of each attribute equals the buff's time).
        /// </summary>
        private bool ServerReadBuff(int bagIndex, ItemStack stack, ItemDefinition scroll)
        {
            if (progress != null && !scroll.CanBeUsedAtLevel(progress.Level))
            {
                MessageRpc(InventoryMessage.LevelTooLow, stack.ItemId, 0);
                return false;
            }
            if (Time.time - health.LastDamageTime < FoodCombatSeconds)
            {
                MessageRpc(InventoryMessage.InCombat, stack.ItemId, 0);
                return false;
            }
            StatusKind kind = BuffKindOf(scroll.buffAttribute);
            if (health.Effects == null || health.Effects.Has(kind))
            {
                MessageRpc(InventoryMessage.NotReady, stack.ItemId, 0);
                return false;
            }
            if (!health.Effects.ServerApply(scroll.displayName, new Color(0.95f, 0.85f, 0.5f), kind, scroll.buffAmount,
                    scroll.buffSeconds, health, harmful: false)) return false;
            bag[bagIndex] = stack.Count > 1 ? stack.WithCount(stack.Count - 1) : ItemStack.Empty;
            return true;
        }

        private System.Collections.IEnumerator ReadReturnScroll(ItemDefinition scroll, Vector2 start)
        {
            while (IsSpawned && ServerNow < returnCastEnd.Value)
            {
                bool moved = Vector2.Distance(AutoAttack.ServerPosition(health), start) > ReturnMoveTolerance;
                bool stopped = health.IsDead || (health.Effects != null && health.Effects.IsSilenced);
                if (moved || stopped)
                {
                    returnCastEnd.Value = 0.0;
                    MessageRpc(InventoryMessage.ReturnInterrupted, catalog.IdOf(scroll), 0);
                    yield break;
                }
                yield return null;
            }
            if (!IsSpawned) yield break;
            returnCastEnd.Value = 0.0;
            Waystone town = Waystone.MainTown;
            if (town == null || !ServerRemoveItem(scroll, 1)) yield break;
            returnReadyAt.Value = ServerNow + scroll.useCooldown;
            if (TryGetComponent(out RPG.Networking.PlayerNetworkMovement movement)) movement.ServerTeleport(town.ArrivalPoint);
            MessageRpc(InventoryMessage.Returned, catalog.IdOf(scroll), 0);
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
        /// <param name="expectedAffix">The quality the buyer saw; when the vendor restocked since (another quality now), the
        /// sale is refused and the buyer gets the new stock. -1: any.</param>
        public bool ServerBuy(Vendor vendor, int stockIndex, int expectedAffix = -1)
        {
            if (!IsServer || health.IsDead || !IsNear(vendor)) return false;
            ItemDefinition[] stock = vendor.Definition.stock;
            if (stockIndex < 0 || stockIndex >= stock.Length || stock[stockIndex] == null) return false;

            ItemDefinition item = stock[stockIndex];
            if (vendor.ServerRemaining(stockIndex) == 0)
            {
                MessageRpc(InventoryMessage.SoldOut, 0, 0);
                VendorStockRpc(vendor.Index, vendor.ServerStockSnapshot(), vendor.ServerAffixSnapshot());
                return false;
            }
            int affixId = vendor.ServerAffix(stockIndex);
            if (expectedAffix >= 0 && expectedAffix != affixId)
            {
                VendorStockRpc(vendor.Index, vendor.ServerStockSnapshot(), vendor.ServerAffixSnapshot());
                return false;
            }
            AffixDefinition affix = affixes != null ? affixes.Get(affixId) : null;
            int price = BuyPrice(item, affix);
            if (gold.Value < price)
            {
                MessageRpc(InventoryMessage.NotEnoughGold, 0, 0);
                return false;
            }
            // Gear comes with the sockets of its quality, like the source game's.
            int bought = affix != null ? affixId : 0;
            if (ServerAddItemWithSockets(item, 1, true, bought, LootDrop.RollSockets(item, affixes, bought)) <= 0) return false;
            gold.Value -= price;
            vendor.ServerTake(stockIndex);
            VendorStockRpc(vendor.Index, vendor.ServerStockSnapshot(), vendor.ServerAffixSnapshot());
            return true;
        }

        /// <summary>How many slots the transmute window has (the source game's 4 x 3 grid).</summary>
        public const int TransmuteSlots = 12;

        /// <summary>The items in these bag slots and how many of each (each slot counted once).</summary>
        public Dictionary<ItemDefinition, int> CountItems(IEnumerable<int> bagSlots)
        {
            var counts = new Dictionary<ItemDefinition, int>();
            var seen = new HashSet<int>();
            foreach (int slot in bagSlots)
            {
                if (slot < 0 || slot >= bag.Count || !seen.Add(slot)) continue;
                ItemDefinition item = Definition(bag[slot]);
                if (item == null) continue;
                counts.TryGetValue(item, out int count);
                counts[item] = count + bag[slot].Count;
            }
            return counts;
        }

        /// <summary>
        /// Server: the source game's transmutation. The items the player put into the transmuter's grid (these bag slots)
        /// make the recipe they match: exactly its kinds of items, at least as many of each. One batch is used up and
        /// the result put in the bag. False (nothing used) when nothing matches or there is no room.
        /// </summary>
        public bool ServerTransmute(Vendor vendor, int[] bagSlots)
        {
            if (!IsServer || health.IsDead || vendor == null || vendor.Definition == null || !vendor.Definition.Transmutes
                || !IsNear(vendor) || bagSlots == null) return false;
            Dictionary<ItemDefinition, int> placed = CountItems(bagSlots);
            Recipe? match = Recipe.Match(vendor.Definition.recipes, placed);
            if (match == null)
            {
                MessageRpc(InventoryMessage.NoRecipe, 0, 0);
                return false;
            }
            Recipe recipe = match.Value;

            foreach (RecipeIngredient ingredient in recipe.ingredients) ServerRemoveItem(ingredient.item, ingredient.count);
            int count = Mathf.Max(1, recipe.resultCount);
            int added = ServerAddItem(recipe.result, count, false);
            if (added < count)
            {
                // No room for all of it: take back what went in and return the items.
                if (added > 0) ServerRemoveItem(recipe.result, added);
                foreach (RecipeIngredient ingredient in recipe.ingredients) ServerAddItem(ingredient.item, ingredient.count, false);
                MessageRpc(InventoryMessage.BagFull, catalog.IdOf(recipe.result), 0);
                return false;
            }
            MessageRpc(InventoryMessage.Crafted, catalog.IdOf(recipe.result), 0);
            return true;
        }

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

            /// <summary>The player's level is below the item's; the item travels in the item id field.</summary>
            LevelTooLow,
            /// <summary>A piece was crafted: the item and its affix.</summary>
            Crafted,
            /// <summary>Too few materials: the material travels in the item id field, how many are needed in the affix field.</summary>
            NotEnoughMaterials,
            /// <summary>The items in the transmuter match no recipe.</summary>
            NoRecipe,
            /// <summary>Picking a lock: the Lockpicking needed travels in the item id field, the player's in the affix field.</summary>
            PickingLock,
            LockpickingTooLow,
            /// <summary>A Tome was read; it travels in the item id field.</summary>
            LearnedSpell,
            AlreadyKnown,
            /// <summary>A Scroll of Returning is being read; it travels in the item id field.</summary>
            Returning,
            ReturnInterrupted,
            Returned,
            /// <summary>Food cannot be eaten in combat.</summary>
            InCombat,
            /// <summary>An orb empowered an item (it travels in the item id field, its new steps in the affix field).</summary>
            Empowered,
            EmpowerFailed,
            CannotEmpower,
            /// <summary>No empowerment point left (affix field 1: an Arcane orb on gear that still has points, or at its limit).</summary>
            NoEmpowerPoints,
        }
    }
}
