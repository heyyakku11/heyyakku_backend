using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Interfaces
{
    public interface ICheckRepository
    {
        /// <summary>
        /// Active checks (with options) already live: scheduled for <paramref name="throughWeek"/> or earlier.
        /// Earliest week first, then sequence.
        /// </summary>
        Task<List<Check>> GetLiveAsync(DateOnly throughWeek, CancellationToken cancellationToken = default);

        /// <summary>An active check that is live as of <paramref name="throughWeek"/>.</summary>
        Task<Check?> GetLiveByIdAsync(Guid id, DateOnly throughWeek, CancellationToken cancellationToken = default);

        /// <summary>The caller's used checks keyed by check id.</summary>
        Task<Dictionary<Guid, UserCheck>> GetUserChecksByCheckAsync(Guid userId, CancellationToken cancellationToken = default);

        /// <summary>The caller's made checks (check, options and votes loaded), earliest first.</summary>
        Task<List<UserCheck>> GetUserChecksAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<UserCheck?> GetUserCheckByIdAsync(Guid userCheckId, CancellationToken cancellationToken = default);

        Task<UserCheck?> GetUserCheckByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default);

        /// <summary>Adds the user/check mapping. Throws AlreadyUsed when the user already used the check.</summary>
        Task<bool> TryAddUserCheckAsync(UserCheck userCheck, CancellationToken cancellationToken = default);

        /// <summary>Adds the vote. Throws AlreadyVoted when the voter already answered this item.</summary>
        Task AddVoteAsync(UserCheckVote vote, CancellationToken cancellationToken = default);

        // Admin scheduling

        /// <summary>All checks (any status), optionally only those scheduled for one week.</summary>
        Task<List<Check>> GetAllAsync(DateOnly? weekStart, CancellationToken cancellationToken = default);

        /// <summary>A tracked check with its options, for editing.</summary>
        Task<Check?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

        Task<int> NextSequenceNoAsync(DateOnly weekStart, CancellationToken cancellationToken = default);

        Task<bool> HasUserChecksAsync(Guid checkId, CancellationToken cancellationToken = default);

        Task AddCheckAsync(Check check, CancellationToken cancellationToken = default);

        Task RemoveCheckAsync(Check check, CancellationToken cancellationToken = default);

        /// <summary>Saves tracked admin changes; a clash on (week, sequence) becomes a 409.</summary>
        Task SaveChecksAsync(CancellationToken cancellationToken = default);
    }
}
