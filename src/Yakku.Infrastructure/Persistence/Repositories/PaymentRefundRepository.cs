using Microsoft.EntityFrameworkCore;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public sealed class PaymentRefundRepository : IPaymentRefundRepository
    {
        private readonly YakkuDbContext _context;

        public PaymentRefundRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<PaymentAttempt?> FindAttemptAsync(Guid attemptId, CancellationToken cancellationToken = default)
        {
            return _context.PaymentAttempts.FirstOrDefaultAsync(item => item.Id == attemptId, cancellationToken);
        }

        public Task<PaymentOrder?> FindOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return _context.PaymentOrders.AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);
        }

        public Task<PaymentRefund?> FindProcessedByAttemptAsync(
            Guid attemptId,
            CancellationToken cancellationToken = default)
        {
            return _context.PaymentRefunds
                .FirstOrDefaultAsync(
                    item => item.PaymentAttemptId == attemptId && item.Status == PaymentRefundStatus.Processed,
                    cancellationToken);
        }

        public async Task AddAsync(PaymentRefund refund, CancellationToken cancellationToken = default)
        {
            _context.PaymentRefunds.Add(refund);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
