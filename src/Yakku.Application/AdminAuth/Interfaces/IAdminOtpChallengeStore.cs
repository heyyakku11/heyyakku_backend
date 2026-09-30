using Yakku.Application.Auth.Models;

namespace Yakku.Application.AdminAuth.Interfaces
{
    public interface IAdminOtpChallengeStore
    {
        Task<OtpChallenge?> GetAsync(string email, CancellationToken cancellationToken = default);
        Task SetAsync(string email, OtpChallenge challenge, TimeSpan ttl, CancellationToken cancellationToken = default);
        Task<bool> ReplaceKeepingTtlAsync(string email, OtpChallenge challenge, CancellationToken cancellationToken = default);
        Task DeleteAsync(string email, CancellationToken cancellationToken = default);
    }
}
