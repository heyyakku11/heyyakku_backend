using System.Security.Cryptography;
using System.Text;
using Yakku.Application.Guests.Interfaces;

namespace Yakku.Application.Guests.Services
{
    public sealed class GuestSessionTokenFactory : IGuestSessionTokenFactory
    {
        private readonly byte[] _secret;

        public GuestSessionTokenFactory(string secret)
        {
            if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32)
            {
                throw new InvalidOperationException(
                    "JWT_SECRET must be at least 32 characters. Add it to your .env file.");
            }

            _secret = Encoding.UTF8.GetBytes(secret);
        }

        public string FromIdempotencyKey(string idempotencyKey)
        {
            var payload = Encoding.UTF8.GetBytes("guest-idem:" + idempotencyKey);
            var mac = HMACSHA256.HashData(_secret, payload);
            return Convert.ToHexString(mac);
        }
    }
}
