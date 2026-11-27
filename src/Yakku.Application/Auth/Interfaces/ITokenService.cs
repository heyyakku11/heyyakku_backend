namespace Yakku.Application.Auth.Interfaces
{
    public interface ITokenService
    {
        string CreateAccessToken(Guid userId, Guid sessionId);

        /// <summary>Same token with an explicit lifetime (the website login uses a fixed 15 minutes).</summary>
        string CreateAccessToken(Guid userId, Guid sessionId, TimeSpan lifetime);
        string CreateAdminAccessToken(Guid adminId, Guid sessionId, string email);
    }
}
