using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace RPG.Accounts
{
    /// <summary>
    /// Server: checks logins, creates accounts and keeps track of who is online. Lives next to the NetworkManager;
    /// <see cref="RPG.Networking.NetworkBootstrap"/> asks it during connection approval. When it is present,
    /// every connection needs a valid login.
    /// Accounts are stored as JSON files on servers and hosts, and in the browser's storage when playing offline in a browser.
    /// </summary>
    public class AccountService : MonoBehaviour
    {
        public const int MinNameLength = 3;
        public const int MaxNameLength = 16;
        public const int MinPasswordLength = 4;

        private static readonly Regex ValidName = new Regex("^[A-Za-z0-9_]+$");

        private IAccountStore store;
        private readonly Dictionary<ulong, AccountRecord> online = new Dictionary<ulong, AccountRecord>();

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

        /// <summary>
        /// Checks a login (or creates the account) without logging it in. Returns null when it is fine,
        /// otherwise a message for the player.
        /// </summary>
        public string Check(LoginRequest request)
        {
            return Check(request, out _);
        }

        /// <summary>Server: checks a login and marks the account as online for this client. Null when accepted.</summary>
        public string LogIn(LoginRequest request, ulong clientId)
        {
            string error = Check(request, out AccountRecord record);
            if (error != null) return error;

            if (request.createCharacter && !Store.TryLoad(record.username, out _))
                Store.Save(record);
            online[clientId] = record;
            Debug.Log($"[Accounts] {record.username} logged in (client {clientId}).");
            return null;
        }

        private string Check(LoginRequest request, out AccountRecord record)
        {
            record = null;
            if (request == null) return "Log in first.";

            string name = (request.username ?? "").Trim();
            if (name.Length < MinNameLength || name.Length > MaxNameLength || !ValidName.IsMatch(name))
                return $"Name: {MinNameLength}-{MaxNameLength} letters, digits or _.";
            if ((request.password ?? "").Length < MinPasswordLength)
                return $"Password: at least {MinPasswordLength} characters.";
            if (IsOnline(name))
                return "This character is already in the game.";

            bool exists = Store.TryLoad(name, out AccountRecord existing);
            if (request.createCharacter)
            {
                if (exists) return "That name is taken.";
                string salt = PasswordHasher.NewSalt();
                record = new AccountRecord
                {
                    username = name,
                    salt = salt,
                    passwordHash = PasswordHasher.Hash(request.password, salt),
                    character = new CharacterRecord { classIndex = Mathf.Max(0, request.classIndex) },
                };
                return null;
            }

            if (!exists || !PasswordHasher.Verify(request.password, existing.salt, existing.passwordHash))
                return "Wrong name or password.";
            record = existing;
            return null;
        }

        public bool IsOnline(string username)
        {
            string key = NormalizeName(username);
            foreach (AccountRecord record in online.Values)
                if (NormalizeName(record.username) == key) return true;
            return false;
        }

        /// <summary>Server: the account a connected client logged in with.</summary>
        public bool TryGetAccount(ulong clientId, out AccountRecord record) => online.TryGetValue(clientId, out record);

        /// <summary>Server: writes an account (with its character) to the store.</summary>
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
