using Yakku.Application.Users.DTOs;

namespace Yakku.Application.Users.Interfaces
{
    public interface IUserService
    {
        Task<UserProfileResponse?> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<UserPollsPage> GetAskedPollsAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default);

        Task<UserPollsPage> GetAnsweredPollsAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default);
    }
}
