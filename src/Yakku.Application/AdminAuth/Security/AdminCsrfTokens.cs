using System.Security.Cryptography;
using System.Text;

namespace Yakku.Application.AdminAuth.Security
{
    /// <summary>
    /// CSRF tokens bound to one session: HMAC-SHA256(server key, session id). The token is not an authentication
    /// credential, so the browser may read it and send it back in the X-CSRF-Token header; a forged cross-site
    /// request cannot know it. It stays valid exactly as long as its session, and a different session gets a
    /// different token. Nothing is stored, and comparison is constant-time.
    /// </summary>
    public sealed class AdminCsrfTokens
    {
        private readonly byte[] _key;

        public AdminCsrfTokens(AdminSessionOptions options)
        {
            if (options.CsrfKey.Length < 32)
            {
                throw new InvalidOperationException("The admin CSRF key must be at least 32 bytes.");
            }

            _key = options.CsrfKey;
        }

        public string Create(Guid sessionId)
        {
            var mac = HMACSHA256.HashData(_key, sessionId.ToByteArray());
            return Convert.ToBase64String(mac).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public bool IsValid(Guid sessionId, string? token)
        {
            if (string.IsNullOrEmpty(token) || token.Length > 128)
            {
                return false;
            }

            var expected = Encoding.UTF8.GetBytes(Create(sessionId));
            var provided = Encoding.UTF8.GetBytes(token);
            return CryptographicOperations.FixedTimeEquals(expected, provided);
        }
    }
}
