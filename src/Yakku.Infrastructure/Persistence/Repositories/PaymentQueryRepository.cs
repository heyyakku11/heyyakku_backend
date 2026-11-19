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

        public Task<PaymentOrder?> FindOwnedOrderAsync(
            Guid userId,
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return _context.PaymentOrders.AsNoTracking()
                .FirstOrDefaultAsync(order => order.Id == orderId && order.UserId == userId, cancellationToken);
        }

        public async Task<IReadOnlyList<PaymentAttempt>> ListAttemptsForOrderAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return await _context.PaymentAttempts.AsNoTracking()
                .Where(attempt => attempt.PaymentOrderId == orderId)
                .OrderByDescending(attempt => attempt.CreatedAt)
                .ToListAsync(cancellationToken);
        }

        public Task<string?> ResolvePlanCodeAsync(Guid? planId, CancellationToken cancellationToken = default)
        {
            if (planId is null)
            {
                return Task.FromResult<string?>(null);
            }

            return _context.Plans.AsNoTracking()
                .Where(plan => plan.Id == planId)
                .Select(plan => plan.Code)
                .FirstOrDefaultAsync(cancellationToken)!;
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
