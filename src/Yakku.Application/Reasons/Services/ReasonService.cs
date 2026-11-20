using Microsoft.Extensions.Options;
using Yakku.Application.Caching;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Application.Reasons.DTOs;
using Yakku.Application.Reasons.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Application.Reasons.Services
{
    public class ReasonService : IReasonService
    {
        private readonly IReasonRepository _repository;
        private readonly IResponseCache _cache;
        private readonly ResponseCacheOptions _cacheOptions;

        public ReasonService(
            IReasonRepository repository,
            IResponseCache? cache = null,
            IOptions<ResponseCacheOptions>? cacheOptions = null)
        {
            _repository = repository;
            _cache = cache ?? new NullResponseCache();
            _cacheOptions = cacheOptions?.Value ?? new ResponseCacheOptions();
        }

        public Task<ReasonListResponse> GetPollOptionReasonsAsync(
            Guid pollId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                ResponseCacheKeys.PollOptionReasons(pollId, optionId, ownerId),
                _cacheOptions.Reasons,
                async ct => await _repository.GetPollOptionReasonsAsync(pollId, ownerId, optionId, ct)
                    ?? throw NotFound(),
                cancellationToken);
        }

        public Task<ReasonListResponse> GetCheckOptionReasonsAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                ResponseCacheKeys.CheckOptionReasons(userCheckId, optionId, ownerId),
                _cacheOptions.Reasons,
                async ct => await _repository.GetCheckOptionReasonsAsync(userCheckId, ownerId, optionId, ct)
                    ?? throw NotFound(),
                cancellationToken);
        }

        public Task<CustomAnswerListResponse> GetPollCustomAnswersAsync(
            Guid pollId,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                ResponseCacheKeys.PollCustomAnswers(pollId, ownerId),
                _cacheOptions.Reasons,
                async ct => await _repository.GetPollCustomAnswersAsync(pollId, ownerId, ct)
                    ?? throw NotFound(),
                cancellationToken);
        }

        public Task<CustomAnswerListResponse> GetCheckCustomAnswersAsync(
            Guid userCheckId,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                ResponseCacheKeys.CheckCustomAnswers(userCheckId, ownerId),
                _cacheOptions.Reasons,
                async ct => await _repository.GetCheckCustomAnswersAsync(userCheckId, ownerId, ct)
                    ?? throw NotFound(),
                cancellationToken);
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
