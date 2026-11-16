using Yakku.Application.AdminReasonReports.DTOs;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminReasonReports.Interfaces
{
    /// <summary>The result of an admin's decision on a report.</summary>
    public enum ReasonReportDecisionResult
    {
        Decided,
        NotFound,

        /// <summary>The report was already approved or rejected.</summary>
        AlreadyDecided
    }

    public interface IAdminReasonReportRepository
    {
        /// <summary>Reports, newest first, one page at a time. A null status returns every status.</summary>
        Task<AdminReasonReportPage> GetReportsAsync(
            ReasonReportStatus? status, int page, int pageSize, CancellationToken cancellationToken = default);

        /// <summary>
        /// Upholds a pending report: the reason text is removed from its vote (so it is no longer shown or counted;
        /// the report keeps a snapshot) and any Ask Back conversation about it is closed, in one save.
        /// </summary>
        Task<ReasonReportDecisionResult> ApproveAsync(Guid reportId, DateTime utcNow, CancellationToken cancellationToken = default);

        /// <summary>Dismisses a pending report. The reason stays on its vote.</summary>
        Task<ReasonReportDecisionResult> RejectAsync(Guid reportId, DateTime utcNow, CancellationToken cancellationToken = default);
    }

    public interface IAdminReasonReportService
    {
        Task<(List<AdminReasonReportResponse> Items, PaginationMeta Meta)> ListAsync(
            string? status, int page, int pageSize, CancellationToken cancellationToken = default);

        Task ApproveAsync(Guid reportId, CancellationToken cancellationToken = default);

        Task RejectAsync(Guid reportId, CancellationToken cancellationToken = default);
    }
}
