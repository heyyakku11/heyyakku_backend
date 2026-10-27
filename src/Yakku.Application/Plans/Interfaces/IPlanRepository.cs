using Yakku.Domain.Entities;

namespace Yakku.Application.Plans.Interfaces
{
    public interface IPlanRepository
    {
        /// <summary>All plans, in plan order.</summary>
        Task<List<Plan>> GetAllAsync(CancellationToken cancellationToken = default);

        /// <summary>A tracked plan, for editing.</summary>
        Task<Plan?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default);

        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
