using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using RPG.Characters;
using RPG.Items;
using Unity.Netcode;
using UnityEngine;

namespace RPG.Accounts
{
    /// <summary>
    /// Server: accounts and their characters. Checks logins, creates accounts, lists, creates and deletes an account's
    /// characters (<see cref="Handle"/>), and keeps track of who is online as which character. Lives next to the
    /// NetworkManager; <see cref="RPG.Networking.NetworkBootstrap"/> asks it during connection approval. When it is present,
    /// every connection needs a valid login.
    /// Accounts are stored as JSON files on servers and hosts, and in the browser's storage when playing offline in a browser.
    /// </summary>
    public class AccountService : MonoBehaviour
    {
        public const int MinNameLength = 3;
        public const int MaxNameLength = 16;
        public const int MinPasswordLength = 4;
        public const int MaxCharacters = 6;

        private static readonly Regex ValidName = new Regex("^[A-Za-z0-9_]+$");

        /// <summary>Who is online: the account and the name of the character it plays.</summary>
        private struct Session
        {
            public AccountRecord Account;
            public string Character;
        }

        private IAccountStore store;
        private readonly Dictionary<ulong, Session> online = new Dictionary<ulong, Session>();
        private Inventory gearSource;

        public IAccountStore Store
        {
            get
            {
                if (store == null) store = CreateDefaultStore();
                return store;
            }
        }

        /// <summary>Replaces where accounts are stored (tests use <see cref="MemoryAccountStore"/>).</summary>
        public void UseStore(IAccountStore accountStore)
        {
            store = accountStore;
        }

        private static IAccountStore CreateDefaultStore()
        {
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                return new PlayerPrefsAccountStore();

            var files = new FileAccountStore(Path.Combine(Application.persistentDataPath, "Accounts"));
            Debug.Log($"[Accounts] Saving accounts in {files.Folder}");
            return files;
        }

        public static string NormalizeName(string username) => (username ?? "").Trim().ToLowerInvariant();

        /// <summary>Null when <paramref name="name"/> may name an account or a character, else why not.</summary>
        public static string CheckName(string name, string what)
        {
            name = (name ?? "").Trim();
            return name.Length < MinNameLength || name.Length > MaxNameLength || !ValidName.IsMatch(name)
                ? $"{what}: {MinNameLength}-{MaxNameLength} letters, digits or _."
                : null;
        }

        /// <summary>
        /// Checks a login to play (or a quick start) without logging it in. Returns null when it is fine, otherwise a
        /// message for the player.
        /// </summary>
        public string Check(LoginRequest request)
        {
            return CheckPlay(request, out _, out _);
        }

        /// <summary>Server: checks a login to play and marks the account as online as its character. Null when accepted.</summary>
        public string LogIn(LoginRequest request, ulong clientId)
        {
            string error = CheckPlay(request, out AccountRecord record, out CharacterRecord character);
            if (error != null) return error;

            Store.Save(record);
            online[clientId] = new Session { Account = record, Character = character.name };
            Debug.Log($"[Accounts] {record.username} entered the world as {character.name} (client {clientId}).");
            return null;
        }

        /// <summary>
        /// An account action other than playing: create the account, list, create or delete a character. Returns the
        /// account's characters, or an error.
        /// </summary>
        public AccountReply Handle(LoginRequest request)
        {
            if (request == null) return AccountReply.Failed("Log in first.");
            if (request.action == AccountAction.Play) return AccountReply.Failed("Choose a character to play.");

            string error = CheckName(request.username, "Account name");
            if (error != null) return AccountReply.Failed(error);
            if ((request.password ?? "").Length < MinPasswordLength)
                return AccountReply.Failed($"Password: at least {MinPasswordLength} characters.");

            if (request.action == AccountAction.Register)
            {
                if (Store.TryLoad(request.username.Trim(), out _)) return AccountReply.Failed("That account name is taken.");
                AccountRecord created = NewAccount(request);
                Store.Save(created);
                Debug.Log($"[Accounts] Account {created.username} created.");
                return Reply(created);
            }

            if (!TryOpen(request, out AccountRecord record)) return AccountReply.Failed("Wrong account name or password.");
            switch (request.action)
            {
                case AccountAction.CreateCharacter:
                    error = CheckName(request.character, "Character name");
                    if (error != null) return AccountReply.Failed(error);
                    if (record.Find(request.character) != null) return AccountReply.Failed("You already have a character with that name.");
                    if (record.characters.Length >= MaxCharacters) return AccountReply.Failed($"An account holds at most {MaxCharacters} characters.");
                    record.characters = Append(record.characters, NewCharacter(request.character.Trim(), request.classIndex, request.female));
                    Store.Save(record);
                    break;
                case AccountAction.DeleteCharacter:
                    CharacterRecord doomed = record.Find(request.character);
                    if (doomed == null) return AccountReply.Failed("There is no such character.");
                    if (IsOnline(record.username)) return AccountReply.Failed("Leave the game first.");
                    record.characters = Array.FindAll(record.characters, c => c != doomed);
                    Store.Save(record);
                    Debug.Log($"[Accounts] {record.username} deleted the character {doomed.name}.");
                    break;
            }
            return Reply(record);
        }

        private string CheckPlay(LoginRequest request, out AccountRecord record, out CharacterRecord character)
        {
            record = null;
            character = null;
            if (request == null) return "Log in first.";

            string error = CheckName(request.username, "Account name");
            if (error != null) return error;
            if ((request.password ?? "").Length < MinPasswordLength)
                return $"Password: at least {MinPasswordLength} characters.";
            if (IsOnline(request.username))
                return "This account is already in the game.";

            if (request.createCharacter && !Store.TryLoad(request.username.Trim(), out _))
            {
                // Quick start: a new account with one character named like it.
                record = NewAccount(request);
                record.characters = new[] { NewCharacter(record.username, request.classIndex, request.female) };
                character = record.characters[0];
                return null;
            }

            if (!TryOpen(request, out record)) return "Wrong account name or password.";
            string wanted = string.IsNullOrEmpty(request.character) && request.createCharacter ? record.username : request.character;
            character = record.Find(wanted);
            if (character == null && request.createCharacter && record.characters.Length < MaxCharacters)
            {
                character = NewCharacter(record.username, request.classIndex, request.female);
                record.characters = Append(record.characters, character);
            }
            if (character == null) return string.IsNullOrEmpty(wanted) ? "Choose a character to play." : "There is no such character.";
            return null;
        }

        /// <summary>Loads an account when the password is right (moving an old single character into its list).</summary>
        private bool TryOpen(LoginRequest request, out AccountRecord record)
        {
            if (!Store.TryLoad((request.username ?? "").Trim(), out record)) return false;
            if (!PasswordHasher.Verify(request.password ?? "", record.salt, record.passwordHash))
            {
                record = null;
                return false;
            }
            Upgrade(record);
            return true;
        }

        /// <summary>Accounts saved before they held several characters: their character joins the list, named like the account.</summary>
        public static void Upgrade(AccountRecord record)
        {
            if (record.characters == null) record.characters = Array.Empty<CharacterRecord>();
            CharacterRecord old = record.character;
            if (record.characters.Length == 0 && old != null && old.hasBeenSaved)
            {
                if (string.IsNullOrEmpty(old.name)) old.name = record.username;
                record.characters = new[] { old };
            }
            record.character = new CharacterRecord();
        }

        private static AccountRecord NewAccount(LoginRequest request)
        {
            string salt = PasswordHasher.NewSalt();
            return new AccountRecord
            {
                username = request.username.Trim(),
                salt = salt,
                passwordHash = PasswordHasher.Hash(request.password, salt),
            };
        }

        private static CharacterRecord NewCharacter(string name, int classIndex, bool female) =>
            new CharacterRecord { name = name, classIndex = Mathf.Max(0, classIndex), female = female };

        private static CharacterRecord[] Append(CharacterRecord[] list, CharacterRecord added)
        {
            var longer = new CharacterRecord[(list?.Length ?? 0) + 1];
            list?.CopyTo(longer, 0);
            longer[longer.Length - 1] = added;
            return longer;
        }

        private AccountReply Reply(AccountRecord record)
        {
            var summaries = new CharacterSummary[record.characters.Length];
            for (int i = 0; i < summaries.Length; i++) summaries[i] = Summarize(record.characters[i]);
            return new AccountReply { characters = summaries };
        }

        /// <summary>A character as the character screens show it: the looks of its worn gear (its starting gear before it played).</summary>
        public CharacterSummary Summarize(CharacterRecord character)
        {
            var summary = new CharacterSummary
            {
                name = character.name, classIndex = character.classIndex, female = character.female, level = Mathf.Max(1, character.level),
            };
            Inventory gear = GearSource;
            ItemCatalog catalog = gear != null ? gear.Catalog : null;
            if (catalog == null) return summary;

            var slots = (AvatarSlot[])Enum.GetValues(typeof(AvatarSlot));
            summary.looks = new string[slots.Length];
            foreach (AvatarSlot slot in slots)
            {
                ItemSlot itemSlot = HeroAppearance.ItemSlotOf(slot);
                ItemDefinition worn = null;
                if (character.hasBeenSaved)
                {
                    int index = Inventory.IndexOf(itemSlot);
                    if (character.equipment != null && index >= 0 && index < character.equipment.Length)
                        worn = catalog.Get(character.equipment[index].id);
                }
                else
                {
                    foreach (Inventory.ClassGear starting in gear.StartingGear)
                        if (starting.classIndex == character.classIndex && starting.item != null && starting.item.slot == itemSlot)
                            worn = starting.item;
                }
                summary.looks[(int)slot] = worn != null ? worn.appearance ?? "" : "";
            }
            return summary;
        }

        /// <summary>The player prefab's inventory: the item catalog and the starting gear.</summary>
        private Inventory GearSource
        {
            get
            {
                if (gearSource != null) return gearSource;
                var network = GetComponent<NetworkManager>();
                GameObject prefab = network != null && network.NetworkConfig != null ? network.NetworkConfig.PlayerPrefab : null;
                if (prefab != null) gearSource = prefab.GetComponent<Inventory>();
                return gearSource;
            }
        }

        public bool IsOnline(string username)
        {
            string key = NormalizeName(username);
            foreach (Session session in online.Values)
                if (NormalizeName(session.Account.username) == key) return true;
            return false;
        }

        /// <summary>Server: the account a connected client logged in with.</summary>
        public bool TryGetAccount(ulong clientId, out AccountRecord record)
        {
            bool found = online.TryGetValue(clientId, out Session session);
            record = found ? session.Account : null;
            return found;
        }

        /// <summary>Server: the account and the character a connected client plays.</summary>
        public bool TryGetCharacter(ulong clientId, out AccountRecord record, out CharacterRecord character)
        {
            record = null;
            character = null;
            if (!online.TryGetValue(clientId, out Session session)) return false;
            record = session.Account;
            character = record.Find(session.Character);
            return character != null;
        }

        /// <summary>Server: writes an account (with its characters) to the store.</summary>
        public void Save(AccountRecord record)
        {
            if (record != null) Store.Save(record);
        }

        /// <summary>Server: the client left; its character was saved by <see cref="CharacterPersistence"/>.</summary>
        public void LogOut(ulong clientId)
        {
            if (online.Remove(clientId))
                Debug.Log($"[Accounts] Client {clientId} logged out.");
        }

        /// <summary>Server: everyone is logged out (the server stopped).</summary>
        public void LogOutEveryone()
        {
            online.Clear();
        }
    }
}
