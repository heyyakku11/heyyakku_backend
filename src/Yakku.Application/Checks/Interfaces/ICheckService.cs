using Yakku.Application.Checks.DTOs;
using Yakku.Domain.Enums;

namespace Yakku.Application.Checks.Interfaces
{
    public interface ICheckService
    {
        /// <summary>Every check that is live (scheduled for this week or an earlier one), earliest first.</summary>
        Task<List<CheckResponse>> GetChecksAsync(Guid? userId, CancellationToken cancellationToken = default);

        /// <summary>One live check; 404 when it does not exist, is inactive, or is not live yet.</summary>
        Task<CheckResponse> GetByIdAsync(Guid id, Guid? userId, CancellationToken cancellationToken = default);

        /// <summary>Makes a live check the caller's own. <paramref name="kind"/> says which kind of card is being made (weekly check or just-for-fun card); a card of another kind is a 404.</summary>
        Task<UserCheckResponse> MakeAsync(
            Guid userId,
            MakeCheckRequest request,
            CheckKind kind = CheckKind.Weekly,
            CancellationToken cancellationToken = default);

        Task<List<UserCheckResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<UserCheckResponse> GetMineByIdAsync(Guid userCheckId, Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Closes the caller's made check so it stops taking answers; 404 when it is not theirs.</summary>
        /// <summary>Opens one of the caller's closed made cards again. closedAt is cleared; a card with an expiry gets a new one of the close time plus 24 hours. 404 when it is not theirs, 409 when it is not closed.</summary>
        Task<UserCheckResponse> ReshareMineAsync(Guid userCheckId, Guid userId, CancellationToken cancellationToken = default);

        Task<UserCheckResponse> CloseMineAsync(Guid userCheckId, Guid userId, CancellationToken cancellationToken = default);

        /// <summary>Deletes the caller's made check with its answers; 404 when it is not theirs.</summary>
        Task DeleteMineAsync(Guid userCheckId, Guid userId, CancellationToken cancellationToken = default);

        Task<UserCheckResponse> GetSharedAsync(string shareCode, Guid? userId, CancellationToken cancellationToken = default);

        Task<CheckVoteResponse> VoteAsync(
            string shareCode,
            Guid userId,
            CastCheckVoteRequest request,
            CancellationToken cancellationToken = default);
    }

    public interface IAdminCheckService
    {
        Task<List<AdminCheckResponse>> ListAsync(DateOnly? weekStart, CancellationToken cancellationToken = default);

        Task<AdminCheckResponse> CreateAsync(SaveCheckRequest request, CancellationToken cancellationToken = default);

        Task<AdminCheckResponse> UpdateAsync(Guid id, SaveCheckRequest request, CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
