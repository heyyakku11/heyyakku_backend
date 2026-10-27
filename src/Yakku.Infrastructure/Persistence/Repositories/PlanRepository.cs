using Microsoft.EntityFrameworkCore;
using Yakku.Application.Plans.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class PlanRepository : IPlanRepository
    {
        private readonly YakkuDbContext _context;

        public PlanRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<List<Plan>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            return _context.Plans
                .AsNoTracking()
                .OrderBy(plan => plan.SortOrder)
                .ToListAsync(cancellationToken);
        }

        public Task<Plan?> GetForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return _context.Plans.FirstOrDefaultAsync(plan => plan.Id == id, cancellationToken);
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
