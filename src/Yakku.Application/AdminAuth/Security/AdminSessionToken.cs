using System.Security.Cryptography;
using System.Text;

namespace Yakku.Application.AdminAuth.Security
{
    /// <summary>The opaque token held in the admin cookie. Only its hash is stored on the server.</summary>
    public static class AdminSessionToken
    {
        public const int ByteLength = 32;

        /// <summary>43 URL-safe base64 characters carry 32 bytes (256 bits) from the OS random generator.</summary>
        public const int EncodedLength = 43;

        public static string Generate()
        {
            return Base64Url(RandomNumberGenerator.GetBytes(ByteLength));
        }

        /// <summary>Cheap shape check before any database lookup, so junk cookies never reach the database.</summary>
        public static bool HasValidShape(string? token)
        {
            if (token is null || token.Length != EncodedLength)
            {
                return false;
            }

            foreach (var c in token)
            {
                var ok = c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9' or '-' or '_';
                if (!ok)
                {
                    return false;
                }
            }

            return true;
        }

        public static string Hash(string token)
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
        }

        private static string Base64Url(byte[] bytes)
        {
            return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }
    }
}
