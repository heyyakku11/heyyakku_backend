using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Reasons.DTOs;
using Yakku.Application.Reasons.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Application.Reasons.Services
{
    public class ReasonService : IReasonService
    {
        private readonly IReasonRepository _repository;

        public ReasonService(IReasonRepository repository)
        {
            _repository = repository;
        }

        public async Task<ReasonListResponse> GetPollOptionReasonsAsync(
            Guid pollId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetPollOptionReasonsAsync(pollId, ownerId, optionId, cancellationToken)
                ?? throw NotFound();
        }

        public async Task<ReasonListResponse> GetCheckOptionReasonsAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetCheckOptionReasonsAsync(userCheckId, ownerId, optionId, cancellationToken)
                ?? throw NotFound();
        }

        public async Task<CustomAnswerListResponse> GetPollCustomAnswersAsync(
            Guid pollId,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetPollCustomAnswersAsync(pollId, ownerId, cancellationToken)
                ?? throw NotFound();
        }

        public async Task<CustomAnswerListResponse> GetCheckCustomAnswersAsync(
            Guid userCheckId,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            return await _repository.GetCheckCustomAnswersAsync(userCheckId, ownerId, cancellationToken)
                ?? throw NotFound();
        }

        public async Task<ReasonReportResponse> ReportPollReasonAsync(
            Guid pollId,
            Guid ownerId,
            Guid reasonId,
            ReportReasonRequest? request,
            CancellationToken cancellationToken = default)
        {
            var reporterReason = Validate(request);
            return await _repository.ReportPollReasonAsync(pollId, ownerId, reasonId, reporterReason, cancellationToken)
                ?? throw NotFound();
        }

        public async Task<ReasonReportResponse> ReportCheckReasonAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid reasonId,
            ReportReasonRequest? request,
            CancellationToken cancellationToken = default)
        {
            var reporterReason = Validate(request);
            return await _repository.ReportCheckReasonAsync(userCheckId, ownerId, reasonId, reporterReason, cancellationToken)
                ?? throw NotFound();
        }

        /// <summary>
        /// The reporter must say why, in text of their choosing (the backend keeps no list of reasons), within the
        /// length limit.
        /// </summary>
        private static string Validate(ReportReasonRequest? request)
        {
            var reason = request?.Reason?.Trim();
            if (string.IsNullOrEmpty(reason))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Say why you are reporting this reason.",
                    "reason");
            }

            if (reason.Length > ReasonReport.MaxReporterReasonLength)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    $"Reason cannot be longer than {ReasonReport.MaxReporterReasonLength} characters.",
                    "reason");
            }

            return reason;
        }

        private static AppException NotFound()
        {
            return new AppException(404, ApiErrorCodes.NotFound, "Reason not found");
        }
    }
}
