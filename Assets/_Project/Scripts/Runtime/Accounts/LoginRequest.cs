using System;
using System.Text;
using UnityEngine;

namespace RPG.Accounts
{
    /// <summary>What a client sends when it connects (the connection payload): log in, or create an account and character.</summary>
    [Serializable]
    public class LoginRequest
    {
        public string username = "";
        public string password = "";
        public bool createCharacter;
        /// <summary>Class of the new character (only used with <see cref="createCharacter"/>).</summary>
        public int classIndex;
        /// <summary>Whether the new character is a woman (only used with <see cref="createCharacter"/>).</summary>
        public bool female;

        public byte[] ToBytes() => Encoding.UTF8.GetBytes(JsonUtility.ToJson(this));

        /// <summary>Null when the payload is missing or not a login request.</summary>
        public static LoginRequest FromBytes(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > 1024) return null;
            try
            {
                return JsonUtility.FromJson<LoginRequest>(Encoding.UTF8.GetString(payload));
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
