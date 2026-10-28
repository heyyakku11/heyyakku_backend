using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Interfaces
{
    public interface ICheckRepository
    {
        /// <summary>
        /// Active checks (with options) scheduled for the week starting <paramref name="weekStart"/> (Monday to Sunday).
        /// Oldest first.
        /// </summary>
        Task<List<Check>> GetForWeekAsync(DateOnly weekStart, CancellationToken cancellationToken = default);

        /// <summary>An active check that is live as of <paramref name="throughWeek"/>.</summary>
        Task<Check?> GetLiveByIdAsync(Guid id, DateOnly throughWeek, CancellationToken cancellationToken = default);

        /// <summary>The caller's used checks keyed by check id.</summary>
        Task<Dictionary<Guid, UserCheck>> GetUserChecksByCheckAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>The caller's made checks (check, options and votes loaded), earliest first.</summary>
        Task<List<UserCheck>> GetUserChecksAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>The user's made checks, newest first, after the cursor (created-at, id). Used by "My asks".</summary>
        Task<List<UserCheck>> GetUserChecksPageAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default);

        /// <summary>A tracked made check owned by <paramref name="userId"/>, for closing or deleting; null when not theirs.</summary>
        Task<UserCheck?> GetOwnedUserCheckForUpdateAsync(Guid userCheckId, Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Deletes the made check; its answers and share code registry row cascade.</summary>
        Task DeleteUserCheckAsync(UserCheck userCheck, CancellationToken cancellationToken = default);

        Task<UserCheck?> GetUserCheckByIdAsync(Guid userCheckId, CancellationToken cancellationToken = default);

        Task<UserCheck?> GetUserCheckByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default);

        /// <summary>Adds the user/check mapping and the maker's own answer in one save. False when the share code was taken; throws AlreadyUsed when the user already used the check.</summary>
        /// <paramref name="afterAdded"/> runs in the same transaction after the rows are saved and before commit; if it throws, everything rolls back.
        Task<bool> TryAddUserCheckAsync(
            UserCheck userCheck,
            UserCheckVote ownerAnswer,
            Func<CancellationToken, Task>? afterAdded = null,
            CancellationToken cancellationToken = default);

        /// <summary>Adds the vote. Throws AlreadyVoted when the voter already answered this item.</summary>
        Task AddVoteAsync(UserCheckVote vote, CancellationToken cancellationToken = default);

        // Admin scheduling

        /// <summary>All checks (any status), optionally only those scheduled for one week.</summary>
        Task<List<Check>> GetAllAsync(DateOnly? weekStart, CancellationToken cancellationToken = default);

        /// <summary>A tracked check with its options, for editing.</summary>
        Task<Check?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);


        Task<bool> HasUserChecksAsync(Guid checkId, CancellationToken cancellationToken = default);

        Task AddCheckAsync(Check check, CancellationToken cancellationToken = default);

        Task RemoveCheckAsync(Check check, CancellationToken cancellationToken = default);

        /// <summary>Saves tracked admin changes; saves the changes.</summary>
        Task SaveChecksAsync(CancellationToken cancellationToken = default);
    }
}
