using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Plans.DTOs;
using Yakku.Application.Plans.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Application.Plans.Services
{
    public class PlanService : IPlanService
    {
        private readonly IPlanRepository _repository;
        private readonly IValidator<UpdatePlanRequest> _validator;

        public PlanService(IPlanRepository repository, IValidator<UpdatePlanRequest> validator)
        {
            _repository = repository;
            _validator = validator;
        }

        public async Task<List<PlanResponse>> ListAsync(CancellationToken cancellationToken = default)
        {
            var plans = await _repository.GetAllAsync(cancellationToken);
            return plans.Select(ToResponse).ToList();
        }

        public async Task<PlanResponse> UpdateAsync(
            Guid id,
            UpdatePlanRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var plan = await _repository.GetForUpdateAsync(id, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Plan not found");

            plan.Update(request.Name?.Trim(), request.PriceMinor, request.IsActive);
            await _repository.SaveAsync(cancellationToken);
            return ToResponse(plan);
        }

        private static PlanResponse ToResponse(Plan plan)
        {
            return new PlanResponse
            {
                Id = plan.Id,
                Code = plan.Code,
                Name = plan.Name,
                PriceMinor = plan.PriceMinor,
                Currency = plan.Currency
            };
        }
    }
}
