using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Reasons.DTOs;
using Yakku.Application.Reasons.Interfaces;

namespace Yakku.Application.Reasons.Services
{
    public class ReasonService : IReasonService
    {
        private readonly IReasonRepository _repository;

        public ReasonService(IReasonRepository repository)
        {
            _repository = repository;
        }

        public async Task<List<ReasonResponse>> GetPollOptionReasonsAsync(
            Guid pollId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetPollOptionReasonsAsync(pollId, ownerId, optionId, cancellationToken)
                ?? throw NotFound();
        }

        public async Task<List<ReasonResponse>> GetCheckOptionReasonsAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetCheckOptionReasonsAsync(userCheckId, ownerId, optionId, cancellationToken)
                ?? throw NotFound();
        }

        public async Task<ReasonReportResponse> ReportPollReasonAsync(
            Guid pollId,
            Guid ownerId,
            Guid reasonId,
            CancellationToken cancellationToken = default)
        {
            return await _repository.ReportPollReasonAsync(pollId, ownerId, reasonId, cancellationToken)
                ?? throw NotFound();
        }

        public async Task<ReasonReportResponse> ReportCheckReasonAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid reasonId,
            CancellationToken cancellationToken = default)
        {
            return await _repository.ReportCheckReasonAsync(userCheckId, ownerId, reasonId, cancellationToken)
                ?? throw NotFound();
        }

        private static AppException NotFound()
        {
            return new AppException(404, ApiErrorCodes.NotFound, "Reason not found");
        }
    }
}
