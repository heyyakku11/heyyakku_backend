using Yakku.Application.Users.DTOs;

namespace Yakku.Application.Users.Interfaces
{
    public sealed record AccountPurgeSummary(int Examined, int Deleted, int Skipped, int Failed);

    public interface IAccountDeletionService
    {
        /// <summary>
        /// Schedules the account for deletion after the grace period and signs the user out everywhere.
        /// Idempotent: a second request keeps the original schedule.
        /// </summary>
        Task<AccountDeletionResponse> RequestDeletionAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        /// <summary>Permanently deletes every account whose grace period has ended.</summary>
        Task<AccountPurgeSummary> PurgeDueAsync(CancellationToken cancellationToken = default);
    }
}
