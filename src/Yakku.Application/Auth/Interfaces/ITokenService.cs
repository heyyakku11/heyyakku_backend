namespace Yakku.Application.Auth.Interfaces
{
    public interface ITokenService
    {
        string CreateAccessToken(Guid userId, Guid sessionId);
        string CreateAdminAccessToken(Guid adminId, Guid sessionId, string email);
    }
}
