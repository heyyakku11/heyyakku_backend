using FluentValidation;
using Microsoft.Extensions.Options;
using Yakku.Application.Caching;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Application.Plans.DTOs;
using Yakku.Application.Plans.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Application.Plans.Services
{
    public class PlanService : IPlanService
    {
        private readonly IPlanRepository _repository;
        private readonly IValidator<UpdatePlanRequest> _validator;
        private readonly IResponseCache _cache;
        private readonly ResponseCacheOptions _cacheOptions;

        public PlanService(
            IPlanRepository repository,
            IValidator<UpdatePlanRequest> validator,
            IResponseCache? cache = null,
            IOptions<ResponseCacheOptions>? cacheOptions = null)
        {
            _repository = repository;
            _validator = validator;
            _cache = cache ?? new NullResponseCache();
            _cacheOptions = cacheOptions?.Value ?? new ResponseCacheOptions();
        }

        public Task<List<PlanResponse>> ListAsync(CancellationToken cancellationToken = default)
        {
            return _cache.GetOrSetAsync(
                ResponseCacheKeys.PlansAll,
                _cacheOptions.Plans,
                async ct =>
                {
                    var plans = await _repository.GetAllAsync(ct);
                    return plans.Select(ToResponse).ToList();
                },
                cancellationToken);
        }

        public async Task<PlanResponse> UpdateAsync(
            Guid id,
            UpdatePlanRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var plan = await _repository.GetForUpdateAsync(id, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Plan not found");

            plan.Update(
                request.Name?.Trim(),
                request.PriceMinor,
                request.IsActive,
                request.DurationDays,
                request.UnlockTickets,
                request.AskBackMessageLimit);
            await _repository.SaveAsync(cancellationToken);
            await _cache.RemoveAsync(ResponseCacheKeys.PlansAll, cancellationToken);
            return ToResponse(plan);
        }

        private static PlanResponse ToResponse(Plan plan)
        {
            return new PlanResponse
            {
                Code = plan.Code,
                Name = plan.Name,
                PriceMinor = plan.PriceMinor,
                Currency = plan.Currency,
                DurationDays = plan.DurationDays,
                UnlockTickets = plan.UnlockTickets,
                AskBackMessageLimit = plan.AskBackMessageLimit
            };
        }
    }
}
