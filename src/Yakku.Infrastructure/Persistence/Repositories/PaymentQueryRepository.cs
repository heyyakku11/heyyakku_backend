using Microsoft.EntityFrameworkCore;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public sealed class PaymentQueryRepository : IPaymentQueryRepository
    {
        private readonly YakkuDbContext _context;

        public PaymentQueryRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<(IReadOnlyList<PaymentOrder> Orders, int TotalCount)> ListOrdersForUserAsync(
            Guid userId,
            int page,
            int pageSize,
            CancellationToken cancellationToken = default)
        {
            var query = _context.PaymentOrders.AsNoTracking()
                .Where(order => order.UserId == userId)
                .OrderByDescending(order => order.CreatedAt);

            var total = await query.CountAsync(cancellationToken);
            var orders = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return (orders, total);
        }

        public Task<string?> ResolvePlanNameAsync(Guid? planId, CancellationToken cancellationToken = default)
        {
            if (planId is null)
            {
                return Task.FromResult<string?>(null);
            }

            return _context.Plans.AsNoTracking()
                .Where(plan => plan.Id == planId)
                .Select(plan => plan.Name)
                .FirstOrDefaultAsync(cancellationToken)!;
        }
    }
}
