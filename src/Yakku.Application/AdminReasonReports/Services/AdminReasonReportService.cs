using Yakku.Application.AdminReasonReports.DTOs;
using Yakku.Application.AdminReasonReports.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminReasonReports.Services
{
    public class AdminReasonReportService : IAdminReasonReportService
    {
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 50;

        private readonly IAdminReasonReportRepository _repository;
        private readonly TimeProvider _time;

        public AdminReasonReportService(IAdminReasonReportRepository repository, TimeProvider time)
        {
            _repository = repository;
            _time = time;
        }

        public async Task<(List<AdminReasonReportResponse> Items, PaginationMeta Meta)> ListAsync(
            string? status, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            ReasonReportStatus? filter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                filter = status.Trim().ToLowerInvariant() switch
                {
                    "pending" => ReasonReportStatus.Pending,
                    "approved" => ReasonReportStatus.Approved,
                    "rejected" => ReasonReportStatus.Rejected,
                    _ => throw new AppException(
                        400, ApiErrorCodes.ValidationError, "Status must be pending, approved or rejected.", "status")
                };
            }

            page = Math.Max(page, 1);
            pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

            var result = await _repository.GetReportsAsync(filter, page, pageSize, cancellationToken);
            return (result.Items, PaginationMeta.Create(page, pageSize, result.TotalCount));
        }

        public async Task ApproveAsync(Guid reportId, CancellationToken cancellationToken = default)
        {
            var result = await _repository.ApproveAsync(reportId, _time.GetUtcNow().UtcDateTime, cancellationToken);
            ThrowUnlessDecided(result);
        }

        public async Task RejectAsync(Guid reportId, CancellationToken cancellationToken = default)
        {
            var result = await _repository.RejectAsync(reportId, _time.GetUtcNow().UtcDateTime, cancellationToken);
            ThrowUnlessDecided(result);
        }

        private static void ThrowUnlessDecided(ReasonReportDecisionResult result)
        {
            switch (result)
            {
                case ReasonReportDecisionResult.NotFound:
                    throw new AppException(404, ApiErrorCodes.NotFound, "Report not found");
                case ReasonReportDecisionResult.AlreadyDecided:
                    throw new AppException(409, ApiErrorCodes.Conflict, "This report has already been decided");
            }
        }
    }
}
