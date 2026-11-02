using Yakku.Application.AdminPolls.DTOs;
using Yakku.Application.AdminPolls.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;

namespace Yakku.Application.AdminPolls.Services
{
    public class AdminPollService : IAdminPollService
    {
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 50;

        private readonly IAdminPollRepository _repository;
        private readonly TimeProvider _time;

        public AdminPollService(IAdminPollRepository repository, TimeProvider time)
        {
            _repository = repository;
            _time = time;
        }

        public async Task<(List<AdminPollResponse> Items, PaginationMeta Meta)> ListAsync(
            string? status, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            AdminPollStatusFilter? filter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                filter = status.Trim().ToLowerInvariant() switch
                {
                    "draft" => AdminPollStatusFilter.Draft,
                    "active" => AdminPollStatusFilter.Active,
                    "closed" => AdminPollStatusFilter.Closed,
                    "expired" => AdminPollStatusFilter.Expired,
                    _ => throw new AppException(
                        400, ApiErrorCodes.ValidationError, "Status must be draft, active, closed or expired.")
                };
            }

            page = Math.Max(page, 1);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            var result = await _repository.GetPollsAsync(
                filter, _time.GetUtcNow().UtcDateTime, page, pageSize, cancellationToken);
            return (result.Items, PaginationMeta.Create(page, pageSize, result.TotalCount));
        }
    }
}
