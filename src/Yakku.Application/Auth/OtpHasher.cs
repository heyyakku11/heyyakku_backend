using System.Security.Cryptography;
using System.Text;

namespace Yakku.Application.Auth
{
    internal static class OtpHasher
    {
        public static string Hash(string email, string otp)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{email}\n{otp}"));
            return Convert.ToHexString(bytes);
        }

        public static bool Verify(string email, string otp, string expectedHash)
        {
            var actual = Hash(email, otp);
            var actualBytes = Encoding.UTF8.GetBytes(actual);
            var expectedBytes = Encoding.UTF8.GetBytes(expectedHash);

            if (actualBytes.Length != expectedBytes.Length)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(actualBytes, expectedBytes);
        }
    }
}
