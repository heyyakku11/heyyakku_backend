using System.Security.Cryptography;
using System.Text;
using Yakku.Application.Guests.Interfaces;

namespace Yakku.Application.Guests.Services
{
    public class GuestTokenHasher : IGuestTokenHasher
    {
        public string Hash(string rawToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes);
        }
    }
}
