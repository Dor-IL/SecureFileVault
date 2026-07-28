using System.Security.Cryptography;

namespace fileVault
{
    static class PasswordHasher
    {
        private const int SaltSize = 16;
        private const int HashSize = 32;
        private const int Iterations = 100_000;

        public static string GenerateSalt()
        {
            byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
            return Convert.ToBase64String(salt);
        }

        public static string HashPassword(string password, string saltBase64)
        {
            byte[] salt = Convert.FromBase64String(saltBase64);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Iterations,
                HashAlgorithmName.SHA256,
                HashSize);

            return Convert.ToBase64String(hash);
        }

        public static bool VerifyPassword(string password, string saltBase64, string storedHashBase64)
        {
            string attemptHash = HashPassword(password, saltBase64);

            byte[] a = Convert.FromBase64String(attemptHash);
            byte[] b = Convert.FromBase64String(storedHashBase64);
            return CryptographicOperations.FixedTimeEquals(a, b);
        }
    }
}
