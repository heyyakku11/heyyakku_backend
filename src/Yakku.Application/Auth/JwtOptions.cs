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

        /// <summary>
        /// Website login: one JWT, fixed at 15 minutes in every environment, with no refresh token. After it ends the
        /// visitor signs in again from the start.
        /// </summary>
        public static readonly TimeSpan WebSessionLifetime = TimeSpan.FromMinutes(15);
        public const int WebSessionExpiresInSeconds = 900;
        public const int RefreshTokenExpiresInSeconds = 2592000;
    }
}
