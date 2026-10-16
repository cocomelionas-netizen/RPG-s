using System;
using RPG.Progression;

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
        /// <summary>Attribute points the player bought, and the experience still to spend on more.</summary>
        public AttributeSet spentPoints;
        public int experienceToSpend;
        public ItemRecord[] bag = Array.Empty<ItemRecord>();
        public ItemRecord[] equipment = Array.Empty<ItemRecord>();
        /// <summary>Rank of every spell of the class (0: not learned) and the spell in each action bar slot (-1: empty).</summary>
        public int[] spellRanks;
        public int[] spellBar;
        /// <summary>Discovered waystones, as bits by waystone id.</summary>
        public int waystones;
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
        /// <summary>Random affix (0 for none).</summary>
        public int affix;
        /// <summary>Each socket: 0 none, -1 empty, above 0 the gem's item id (older saves have only the first).</summary>
        public int socket;
        public int socket2;
        public int socket3;
        public int socket4;

        public ItemRecord(int id, int count, int affix, int socket = 0, int socket2 = 0, int socket3 = 0, int socket4 = 0)
        {
            this.id = id;
            this.count = count;
            this.affix = affix;
            this.socket = socket;
            this.socket2 = socket2;
            this.socket3 = socket3;
            this.socket4 = socket4;
        }
    }
}
