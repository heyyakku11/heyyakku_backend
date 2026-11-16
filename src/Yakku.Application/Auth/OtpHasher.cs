using System.Security.Cryptography;
using System.Text;

namespace Yakku.Application.Auth
{
    public static class OtpHasher
    {
        public static string Hash(string phoneNumber, string otp)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{phoneNumber}\n{otp}"));
            return Convert.ToHexString(bytes);
        }

        public static bool Verify(string phoneNumber, string otp, string expectedHash)
        {
            var actual = Hash(phoneNumber, otp);
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
