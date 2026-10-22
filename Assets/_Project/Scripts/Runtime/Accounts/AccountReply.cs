using System;
using UnityEngine;

namespace RPG.Accounts
{
    /// <summary>What a character looks like on the character screens: name, class, level and the looks it wears.</summary>
    [Serializable]
    public class CharacterSummary
    {
        public string name;
        public int classIndex;
        public bool female;
        public int level;
        /// <summary>The worn gear's looks by avatar slot (<see cref="RPG.Characters.AvatarSlot"/>), empty for bare.</summary>
        public string[] looks = Array.Empty<string>();
    }

    /// <summary>The answer to an account action other than playing: an error, or the account's characters.</summary>
    [Serializable]
    public class AccountReply
    {
        /// <summary>A remote server sends the reply as its refusal's reason, starting with this.</summary>
        public const string Prefix = "ACCOUNT:";

        /// <summary>Null when the action worked.</summary>
        public string error;
        public CharacterSummary[] characters = Array.Empty<CharacterSummary>();

        public static AccountReply Failed(string message) => new AccountReply { error = message };

        public string ToReason() => Prefix + JsonUtility.ToJson(this);

        /// <summary>The reply in a refusal's reason, or null when the reason is not one.</summary>
        public static AccountReply FromReason(string reason)
        {
            if (string.IsNullOrEmpty(reason) || !reason.StartsWith(Prefix, StringComparison.Ordinal)) return null;
            try
            {
                return JsonUtility.FromJson<AccountReply>(reason.Substring(Prefix.Length));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
