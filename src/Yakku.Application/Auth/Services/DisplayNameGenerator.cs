using System.Security.Cryptography;
using Yakku.Application.Auth.Interfaces;

namespace Yakku.Application.Auth.Services
{
    public class DisplayNameGenerator : IDisplayNameGenerator
    {
        public string Generate()
        {
            var number = RandomNumberGenerator.GetInt32(1000, 100_000);
            return $"yakku@{number}";
        }
    }
}
