namespace Yakku.Application.Auth.Interfaces
{
    public interface ITokenService
    {
        string CreateAccessToken(Guid userId);
    }
}
