using Yakku.Application.YakkuDirectory.DTOs;

namespace Yakku.Application.YakkuDirectory.Interfaces
{
    public interface IYakkuDirectoryService
    {
        Task<IReadOnlyList<YakkuUserProfileResponse>> ListUsersAsync(
            string? status,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<YakkuGuestProfileResponse>> ListGuestsAsync(
            string? status,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<YakkuPollResponse>> ListPollsAsync(
            string? status,
            CancellationToken cancellationToken = default);
    }
}
