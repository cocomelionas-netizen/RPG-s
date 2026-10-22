using System;
using System.Text;
using UnityEngine;

namespace RPG.Accounts
{
    /// <summary>What a login asks for.</summary>
    public enum AccountAction
    {
        /// <summary>Enter the world as <see cref="LoginRequest.character"/>.</summary>
        Play,
        /// <summary>Create the account (no characters yet).</summary>
        Register,
        /// <summary>Check the password and list the account's characters.</summary>
        List,
        /// <summary>Add a character (<see cref="LoginRequest.character"/>, class, gender).</summary>
        CreateCharacter,
        /// <summary>Delete the character <see cref="LoginRequest.character"/>.</summary>
        DeleteCharacter,
    }

    /// <summary>
    /// What a client sends when it connects (the connection payload): an account action. Only <see cref="AccountAction.Play"/>
    /// enters the world; the others are answered with an <see cref="AccountReply"/> (as the refusal's reason).
    /// </summary>
    [Serializable]
    public class LoginRequest
    {
        public string username = "";
        public string password = "";
        public AccountAction action;
        /// <summary>The character to play, create or delete.</summary>
        public string character = "";
        /// <summary>
        /// Quick start (bots, tests): with <see cref="AccountAction.Play"/>, creates the account and a character named like it
        /// when they do not exist yet.
        /// </summary>
        public bool createCharacter;
        /// <summary>Class of a new character.</summary>
        public int classIndex;
        /// <summary>Whether a new character is a woman.</summary>
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
