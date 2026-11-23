namespace Yakku.Application.Auth
{
    /// <summary>
    /// Production/Staging JWT lifetime defaults. Runtime values come from
    /// <see cref="Configuration.JwtSettings"/> (Development shortens these in appsettings.Development.json).
    /// </summary>
    public static class JwtOptions
    {
        public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
        public const int AccessTokenExpiresInSeconds = 900;
        public const int RefreshTokenExpiresInSeconds = 2592000;
    }
}
