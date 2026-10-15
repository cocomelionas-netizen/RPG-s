using System;

namespace RPG.Accounts
{
    /// <summary>A stored account: login data and its character. Saved as JSON.</summary>
    [Serializable]
    public class AccountRecord
    {
        public string username;
        /// <summary>Base64 salt and PBKDF2 hash of the password; the password itself is never stored.</summary>
        public string salt;
        public string passwordHash;
        public CharacterRecord character = new CharacterRecord();
    }

    /// <summary>Everything that is kept about a character between sessions.</summary>
    [Serializable]
    public class CharacterRecord
    {
        public int classIndex;
        public int level = 1;
        public int experience;
        public int gold;
        public ItemRecord[] bag = Array.Empty<ItemRecord>();
        public ItemRecord[] equipment = Array.Empty<ItemRecord>();
        /// <summary>False for a new character, which starts at the spawn point with the starting items.</summary>
        public bool hasBeenSaved;
        public float x;
        public float y;
    }

    [Serializable]
    public struct ItemRecord
    {
        public int id;
        public int count;

        public ItemRecord(int id, int count)
        {
            this.id = id;
            this.count = count;
        }
    }
}
