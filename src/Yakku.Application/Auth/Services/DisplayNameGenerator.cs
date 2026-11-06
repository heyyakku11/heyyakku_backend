using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Users.AnonymousNames;

namespace Yakku.Application.Auth.Services
{
    /// <summary>Produces a random anonymous-name candidate such as "SilentTigerMoon". It does not check uniqueness.</summary>
    public class DisplayNameGenerator : IDisplayNameGenerator
    {
        public string Generate() => AnonymousNameFormat.Generate();
    }
}
