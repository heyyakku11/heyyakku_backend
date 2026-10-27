using Yakku.Application.Plans.DTOs;

namespace Yakku.Application.Plans.Interfaces
{
    public interface IPlanService
    {
        Task<List<PlanResponse>> ListAsync(CancellationToken cancellationToken = default);

        Task<PlanResponse> UpdateAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken = default);
    }
}
