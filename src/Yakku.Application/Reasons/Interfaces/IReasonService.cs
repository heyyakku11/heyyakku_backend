using Yakku.Application.Reasons.DTOs;

namespace Yakku.Application.Reasons.Interfaces
{
    public interface IReasonService
    {
        Task<ReasonListResponse> GetPollOptionReasonsAsync(Guid pollId, Guid ownerId, Guid optionId, CancellationToken cancellationToken = default);

        Task<ReasonListResponse> GetCheckOptionReasonsAsync(Guid userCheckId, Guid ownerId, Guid optionId, CancellationToken cancellationToken = default);

        Task<CustomAnswerListResponse> GetPollCustomAnswersAsync(Guid pollId, Guid ownerId, CancellationToken cancellationToken = default);

        Task<CustomAnswerListResponse> GetCheckCustomAnswersAsync(Guid userCheckId, Guid ownerId, CancellationToken cancellationToken = default);

        Task<ReasonReportResponse> ReportPollReasonAsync(Guid pollId, Guid ownerId, Guid reasonId, ReportReasonRequest? request, CancellationToken cancellationToken = default);

        Task<ReasonReportResponse> ReportCheckReasonAsync(Guid userCheckId, Guid ownerId, Guid reasonId, ReportReasonRequest? request, CancellationToken cancellationToken = default);
    }
}
