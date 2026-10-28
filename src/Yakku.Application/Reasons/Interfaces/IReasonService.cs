using Yakku.Application.Reasons.DTOs;

namespace Yakku.Application.Reasons.Interfaces
{
    public interface IReasonService
    {
        Task<List<ReasonResponse>> GetPollOptionReasonsAsync(Guid pollId, Guid ownerId, Guid optionId, CancellationToken cancellationToken = default);

        Task<List<ReasonResponse>> GetCheckOptionReasonsAsync(Guid userCheckId, Guid ownerId, Guid optionId, CancellationToken cancellationToken = default);

        Task ReportPollReasonAsync(Guid pollId, Guid ownerId, Guid reasonId, CancellationToken cancellationToken = default);

        Task ReportCheckReasonAsync(Guid userCheckId, Guid ownerId, Guid reasonId, CancellationToken cancellationToken = default);
    }
}
