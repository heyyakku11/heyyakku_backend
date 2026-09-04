using System.Security.Cryptography;
using System.Text;

namespace Yakku.Application.Auth
{
    internal static class RefreshTokenHasher
    {
        public static string GenerateSecret()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }

        public static string GenerateSalt()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
        }

        public static string Hash(string salt, string secret)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{salt}\n{secret}"));
            return Convert.ToHexString(bytes);
        }

        public static bool Verify(string salt, string secret, string expectedHash)
        {
            var actual = Hash(salt, secret);
            var actualBytes = Encoding.UTF8.GetBytes(actual);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);

            if (actualBytes.Length != expectedBytes.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }

        public static bool TryParse(string refreshToken, out Guid sessionId, out string secret)
        {
            sessionId = Guid.Empty;
            secret = string.Empty;

            if (string.IsNullOrWhiteSpace(refreshToken))
            {
                return false;
            }

            var separator = refreshToken.IndexOf('.');
            if (separator <= 0 || separator == refreshToken.Length - 1)
            {
                return false;
            }

            if (!Guid.TryParse(refreshToken[..separator], out sessionId) || sessionId == Guid.Empty)
            {
                return false;
            }

            secret = refreshToken[(separator + 1)..];
            return secret.Length > 0;
        }
    }
}
