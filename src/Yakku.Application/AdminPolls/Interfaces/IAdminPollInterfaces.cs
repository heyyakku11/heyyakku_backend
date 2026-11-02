using Yakku.Application.AdminPolls.DTOs;
using Yakku.Application.Common.Responses;

namespace Yakku.Application.AdminPolls.Interfaces
{
    public interface IAdminPollRepository
    {
        /// <summary>Polls, newest first, one page at a time. A null status returns every status.</summary>
        Task<AdminPollPage> GetPollsAsync(
            AdminPollStatusFilter? status, DateTime utcNow, int page, int pageSize, CancellationToken cancellationToken = default);
    }

    public interface IAdminPollService
    {
        Task<(List<AdminPollResponse> Items, PaginationMeta Meta)> ListAsync(
            string? status, int page, int pageSize, CancellationToken cancellationToken = default);
    }
}
