namespace Yakku.Application.Auth
{
    public static class JwtOptions
    {
        public static readonly TimeSpan AccessTokenLifetime = TimeSpan.FromMinutes(15);
        public static readonly TimeSpan RefreshTokenLifetime = TimeSpan.FromDays(30);
        public const int AccessTokenExpiresInSeconds = 900;
        public const int RefreshTokenExpiresInSeconds = 2592000;
    }
}
