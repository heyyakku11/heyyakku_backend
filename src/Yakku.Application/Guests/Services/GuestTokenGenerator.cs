using System.Security.Cryptography;
using Yakku.Application.Guests.Interfaces;

namespace Yakku.Application.Guests.Services
{
    public class GuestTokenGenerator : IGuestTokenGenerator
    {
        public string Generate()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        }
    }
}
