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

        public static string? TryRecover(string email, string otpHash)
        {
            if (string.IsNullOrWhiteSpace(otpHash) || otpHash.Length != 64)
            {
                return null;
            }

            var max = (int)Math.Pow(10, OtpOptions.Length);
            for (var i = 0; i < max; i++)
            {
                var otp = i.ToString($"D{OtpOptions.Length}");
                if (string.Equals(Hash(email, otp), otpHash, StringComparison.OrdinalIgnoreCase))
                {
                    return otp;
                }
            }

            return null;
        }
    }
}
