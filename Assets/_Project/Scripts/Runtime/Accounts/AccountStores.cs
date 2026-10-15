using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace RPG.Accounts
{
    /// <summary>Where accounts are kept. Names are compared case-insensitively.</summary>
    public interface IAccountStore
    {
        bool TryLoad(string username, out AccountRecord record);
        void Save(AccountRecord record);
    }

    /// <summary>One JSON file per account in a folder (servers and hosts). A database can replace it later.</summary>
    public class FileAccountStore : IAccountStore
    {
        private readonly string folder;

        public FileAccountStore(string folder)
        {
            this.folder = folder;
            Directory.CreateDirectory(folder);
        }

        public string Folder => folder;

        public bool TryLoad(string username, out AccountRecord record)
        {
            record = null;
            string path = PathFor(username);
            if (!File.Exists(path)) return false;
            try
            {
                record = JsonUtility.FromJson<AccountRecord>(File.ReadAllText(path));
                return record != null;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Accounts] Could not read {path}: {exception.Message}");
                return false;
            }
        }

        public void Save(AccountRecord record)
        {
            string path = PathFor(record.username);
            // Write to a temporary file first so a crash mid-write never leaves a broken account.
            string temporary = path + ".tmp";
            File.WriteAllText(temporary, JsonUtility.ToJson(record, true));
            if (File.Exists(path)) File.Delete(path);
            File.Move(temporary, path);
        }

        private string PathFor(string username) => Path.Combine(folder, AccountService.NormalizeName(username) + ".json");
    }

    /// <summary>Accounts in PlayerPrefs: in a browser this is the browser's own storage (IndexedDB), kept between visits.</summary>
    public class PlayerPrefsAccountStore : IAccountStore
    {
        private const string Prefix = "rpg.account.";

        public bool TryLoad(string username, out AccountRecord record)
        {
            record = null;
            string json = PlayerPrefs.GetString(Prefix + AccountService.NormalizeName(username), "");
            if (string.IsNullOrEmpty(json)) return false;
            record = JsonUtility.FromJson<AccountRecord>(json);
            return record != null;
        }

        public void Save(AccountRecord record)
        {
            PlayerPrefs.SetString(Prefix + AccountService.NormalizeName(record.username), JsonUtility.ToJson(record));
            PlayerPrefs.Save();
        }
    }

    /// <summary>Accounts in memory only (tests).</summary>
    public class MemoryAccountStore : IAccountStore
    {
        private readonly Dictionary<string, string> accounts = new Dictionary<string, string>();

        public bool TryLoad(string username, out AccountRecord record)
        {
            record = accounts.TryGetValue(AccountService.NormalizeName(username), out string json)
                ? JsonUtility.FromJson<AccountRecord>(json)
                : null;
            return record != null;
        }

        public void Save(AccountRecord record)
        {
            accounts[AccountService.NormalizeName(record.username)] = JsonUtility.ToJson(record);
        }
    }
}
