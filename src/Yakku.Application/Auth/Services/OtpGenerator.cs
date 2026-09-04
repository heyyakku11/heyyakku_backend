using System.Security.Cryptography;
using Yakku.Application.Auth.Interfaces;

namespace Yakku.Application.Auth.Services
{
    public class OtpGenerator : IOtpGenerator
    {
        public string Generate()
        {
            var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
            return value.ToString("D6");
        }
    }
}
