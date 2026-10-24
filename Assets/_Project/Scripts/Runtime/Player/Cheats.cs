using System.Collections.Generic;
using RPG.Combat;
using RPG.Items;
using RPG.Networking;
using RPG.Progression;
using RPG.UI;
using RPG.World;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Player
{
    /// <summary>What a cheat does (<see cref="Cheats"/>).</summary>
    public enum CheatCommand : byte
    {
        LevelUp,
        MaxLevel,
        Experience,
        Gold,
        Heal,
        God,
        Speed,
        LearnSpells,
        DiscoverWaystones,
        Teleport,
        KillNearby,
        Potions,
        Gems,
        Gear,
        Orbs,
        ClearBag,
        Keys,
        EmpowerWeapon,
    }

    /// <summary>
    /// On the player: cheats for testing (the F9 window, <see cref="CheatWindow"/>). Only the host's own character may use
    /// them: offline single player, or the one hosting; other players of a hosted game cannot.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Cheats : NetworkBehaviour
    {
        public const int ExperienceGift = 5000;
        public const int GoldGift = 10000;
        private const float KillRadius = 8f;
        private const float GodRefillInterval = 0.5f;
        private static readonly Color MessageColor = new Color(1f, 0.55f, 0.9f);

        private Health health;
        private Mana mana;
        private CharacterProgress progress;
        private Inventory inventory;
        private Spellbook spellbook;
        private float nextRefill;

        /// <summary>Server: takes no damage and keeps full health and mana.</summary>
        public bool God { get; private set; }

        /// <summary>Server: moves three times as fast.</summary>
        public bool Fast { get; private set; }

        /// <summary>Whether this character may cheat (the host's own).</summary>
        public bool Allowed => IsSpawned && NetworkManager != null && NetworkManager.IsHost && OwnerClientId == NetworkManager.LocalClientId;

        private void Awake()
        {
            health = GetComponent<Health>();
            mana = GetComponent<Mana>();
            progress = GetComponent<CharacterProgress>();
            inventory = GetComponent<Inventory>();
            spellbook = GetComponent<Spellbook>();
        }

        public void Request(CheatCommand command, int value = 0)
        {
            if (IsOwner && Allowed) CheatRpc(command, value);
        }

        [Rpc(SendTo.Server)]
        private void CheatRpc(CheatCommand command, int value, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId == OwnerClientId) ServerCheat(command, value);
        }

        /// <summary>Server: runs a cheat. False when this character may not cheat or it did nothing.</summary>
        public bool ServerCheat(CheatCommand command, int value = 0)
        {
            if (!IsServer || !Allowed) return false;
            switch (command)
            {
                case CheatCommand.LevelUp:
                    if (progress == null || progress.Level >= LevelCurve.MaxLevel) return false;
                    progress.ServerAddExperience(Mathf.Max(1, progress.ExperienceToNext - progress.Experience));
                    return true;
                case CheatCommand.MaxLevel:
                    if (progress == null) return false;
                    for (int i = 0; i < LevelCurve.MaxLevel && progress.Level < LevelCurve.MaxLevel; i++)
                        progress.ServerAddExperience(Mathf.Max(1, progress.ExperienceToNext - progress.Experience));
                    return true;
                case CheatCommand.Experience:
                    if (progress == null) return false;
                    progress.ServerAddExperience(ExperienceGift);
                    return true;
                case CheatCommand.Gold:
                    if (inventory == null) return false;
                    inventory.ServerAddGold(GoldGift);
                    return true;
                case CheatCommand.Heal:
                    Refill();
                    StatusEffects effects = health.Effects;
                    if (effects != null) effects.ServerRemoveHarmful();
                    return true;
                case CheatCommand.God:
                    God = !God;
                    health.ServerInvulnerable = God;
                    if (God) Refill();
                    Say(God ? "God mode on" : "God mode off");
                    return true;
                case CheatCommand.Speed:
                    Fast = !Fast;
                    if (health.Effects == null) return false;
                    if (Fast) health.Effects.ServerApply("Cheat Speed", MessageColor, StatusKind.Haste, 200f, 36000f, harmful: false);
                    else health.Effects.ServerRemove(StatusKind.Haste);
                    Say(Fast ? "Fast on" : "Fast off");
                    return true;
                case CheatCommand.LearnSpells:
                    return LearnSpells();
                case CheatCommand.DiscoverWaystones:
                    var traveller = GetComponent<WaystoneTraveller>();
                    if (traveller == null) return false;
                    foreach (Waystone stone in Waystone.All) traveller.ServerDiscover(stone.Id);
                    Say("Every waypoint discovered");
                    return true;
                case CheatCommand.Teleport:
                    Waystone to = Waystone.Get(value);
                    var movement = GetComponent<PlayerNetworkMovement>();
                    if (to == null || movement == null) return false;
                    movement.ServerTeleport(to.ArrivalPoint);
                    return true;
                case CheatCommand.KillNearby:
                    return KillNearby();
                case CheatCommand.Potions:
                    return Give(item => item.use == ItemUse.Heal || item.use == ItemUse.RestoreMana || item.use == ItemUse.Food, 10);
                case CheatCommand.Gems:
                    return Give(item => item.isGem, 3);
                case CheatCommand.Orbs:
                    return Give(item => item.use == ItemUse.Empower || item.use == ItemUse.Combine || item.use == ItemUse.Buff, 1);
                case CheatCommand.Gear:
                    return GiveGear();
                case CheatCommand.ClearBag:
                    return ClearBag();
                case CheatCommand.Keys:
                    return GiveKeys();
                case CheatCommand.EmpowerWeapon:
                    return EmpowerWeapon();
                default:
                    return false;
            }
        }

        private void Update()
        {
            if (!God || !IsServer || Time.time < nextRefill) return;
            nextRefill = Time.time + GodRefillInterval;
            if (health.IsDead) health.ServerRevive(health.Max);
            Refill();
        }

        private void Refill()
        {
            if (health.IsDead) health.ServerRevive(health.Max);
            health.RestoreFull();
            if (mana != null) mana.ServerRestore(mana.Max);
        }

        private bool LearnSpells()
        {
            if (spellbook == null) return false;
            int learned = 0;
            IReadOnlyList<AbilityDefinition> spells = spellbook.Spells;
            for (int i = 0; i < spells.Count; i++)
                if (spellbook.ServerLearn(spells[i])) learned++;
            Say($"Learned {learned} spells");
            return true;
        }

        private bool KillNearby()
        {
            Vector2 at = transform.position;
            var victims = new List<Health>();
            foreach (Health unit in Health.Spawned)
                if (unit != null && unit != health && !unit.IsDead && unit.Faction == Faction.Monster
                    && Vector2.Distance(at, unit.transform.position) <= KillRadius)
                    victims.Add(unit);
            // Its own list: killing changes Health.Spawned (loot, despawning).
            foreach (Health victim in victims)
            {
                victim.ServerInvulnerable = false;
                victim.ApplyDamage(victim.Max * 10, false, health);
            }
            Say($"Killed {victims.Count} monsters");
            return victims.Count > 0;
        }

        /// <summary>Gives <paramref name="count"/> of every catalog item that matches (as many as fit in the bag).</summary>
        private bool Give(System.Predicate<ItemDefinition> matches, int count)
        {
            if (inventory == null || inventory.Catalog == null) return false;
            int given = 0;
            foreach (ItemDefinition item in inventory.Catalog.items)
            {
                if (item == null || !matches(item)) continue;
                // Returns how many fitted: none means the bag is full.
                if (inventory.ServerAddItem(item, count, false) == 0) break;
                given++;
            }
            Say(given > 0 ? $"Gave {given} kinds of items" : "The bag is full");
            return given > 0;
        }

        /// <summary>Gives two of every key a dungeon asks for (Keys to the Barracks, Tower Key, Underhalls Key...).</summary>
        private bool GiveKeys()
        {
            RPG.Dungeons.DungeonManager dungeons = RPG.Dungeons.DungeonManager.Instance;
            if (inventory == null || dungeons == null) return false;
            var keys = new HashSet<ItemDefinition>();
            foreach (RPG.Dungeons.DungeonDefinition dungeon in dungeons.Dungeons)
                if (dungeon != null && dungeon.key != null) keys.Add(dungeon.key);
            return Give(keys.Contains, 2);
        }

        /// <summary>The worn weapon one step more empowered (to see its glow), back to +0 after the most there is (+12).</summary>
        private bool EmpowerWeapon()
        {
            if (inventory == null) return false;
            int steps = inventory.GetEquipped(ItemSlot.Weapon).EmpowerSteps;
            return inventory.ServerSetWornEmpower(ItemSlot.Weapon, steps >= MaxWeaponGlowSteps ? 0 : steps + 1);
        }

        /// <summary>The most steps there are (<see cref="ItemDefinition.MaxEmpowerSteps"/>).</summary>
        public const int MaxWeaponGlowSteps = ItemDefinition.MaxEmpowerSteps;

        /// <summary>Gives one piece of the class's gear of the player's level (or the nearest below) for every slot.</summary>
        private bool GiveGear()
        {
            if (inventory == null || inventory.Catalog == null || progress == null) return false;
            var best = new Dictionary<ItemSlot, ItemDefinition>();
            foreach (ItemDefinition item in inventory.Catalog.items)
            {
                if (item == null || !item.IsEquippable || item.dropWeight <= 0f || item.itemLevel > progress.Level
                    || !item.CanBeUsedByClass(progress.ClassIndex))
                    continue;
                if (!best.TryGetValue(item.slot, out ItemDefinition current) || item.itemLevel > current.itemLevel
                    || (item.itemLevel == current.itemLevel && item.dropWeight > current.dropWeight))
                    best[item.slot] = item;
            }
            int given = 0;
            foreach (ItemDefinition piece in best.Values)
                if (inventory.ServerAddItem(piece, 1, false) > 0) given++;
            Say(given > 0 ? $"Gave {given} pieces of level {progress.Level} gear" : "The bag is full");
            return given > 0;
        }

        private bool ClearBag()
        {
            if (inventory == null || inventory.Catalog == null) return false;
            for (int i = 0; i < inventory.BagSize; i++)
            {
                ItemStack stack = inventory.GetBagSlot(i);
                ItemDefinition item = inventory.Catalog.Get(stack.ItemId);
                if (item != null && stack.Count > 0) inventory.ServerRemoveItem(item, stack.Count);
            }
            Say("Bag emptied");
            return true;
        }

        private void Say(string message) => SayRpc(message);

        [Rpc(SendTo.Owner)]
        private void SayRpc(string message) => MessageLog.Add($"[Cheat] {message}.", MessageColor);
    }
}
