using System;
using System.Security.Cryptography;

namespace RPG.Accounts
{
    /// <summary>Salted PBKDF2 password hashes. Only the salt and the hash are stored.</summary>
    public static class PasswordHasher
    {
        private const int Iterations = 10000;
        private const int HashBytes = 32;

        /// <summary>A new random salt (base64).</summary>
        public static string NewSalt()
        {
            // Two GUIDs: random enough for a salt and available on every platform, including WebGL.
            byte[] salt = new byte[32];
            Buffer.BlockCopy(Guid.NewGuid().ToByteArray(), 0, salt, 0, 16);
            Buffer.BlockCopy(Guid.NewGuid().ToByteArray(), 0, salt, 16, 16);
            return Convert.ToBase64String(salt);
        }

        public static string Hash(string password, string salt)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(password ?? "", Convert.FromBase64String(salt), Iterations))
                return Convert.ToBase64String(pbkdf2.GetBytes(HashBytes));
        }

        public static bool Verify(string password, string salt, string expectedHash)
        {
            if (string.IsNullOrEmpty(salt) || string.IsNullOrEmpty(expectedHash)) return false;
            byte[] actual = Convert.FromBase64String(Hash(password, salt));
            byte[] expected = Convert.FromBase64String(expectedHash);
            if (actual.Length != expected.Length) return false;

            // Compare every byte so the time taken does not reveal how much matched.
            int difference = 0;
            for (int i = 0; i < actual.Length; i++)
                difference |= actual[i] ^ expected[i];
            return difference == 0;
        }
    }
}
