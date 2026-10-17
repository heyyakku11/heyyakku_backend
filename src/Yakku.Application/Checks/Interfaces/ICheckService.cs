using Yakku.Application.Checks.DTOs;

namespace Yakku.Application.Checks.Interfaces
{
    public interface ICheckService
    {
        /// <summary>Every check that is live (scheduled for this week or an earlier one), earliest first.</summary>
        Task<List<CheckResponse>> GetChecksAsync(Guid? userId, CancellationToken cancellationToken = default);

        /// <summary>One live check; 404 when it does not exist, is inactive, or is not live yet.</summary>
        Task<CheckResponse> GetByIdAsync(Guid id, Guid? userId, CancellationToken cancellationToken = default);

        Task<UserCheckResponse> MakeAsync(Guid checkId, Guid userId, CancellationToken cancellationToken = default);

        Task<List<UserCheckResponse>> GetMineAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<UserCheckResponse> GetMineByIdAsync(Guid userCheckId, Guid userId, CancellationToken cancellationToken = default);

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
