using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public sealed class PaymentOrderRepository : IPaymentOrderRepository
    {
        private const string IdempotencyConstraint = "IX_payment_orders_user_idempotency";

        private readonly YakkuDbContext _context;

        public PaymentOrderRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<Plan?> GetActivePurchasablePlanAsync(Guid planId, CancellationToken cancellationToken = default)
        {
            return _context.Plans
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    plan => plan.Id == planId && plan.IsActive && plan.Id != Plan.FreeId,
                    cancellationToken);
        }

        public Task<PaymentOrder?> FindByIdempotencyKeyAsync(
            Guid userId,
            string idempotencyKey,
            CancellationToken cancellationToken = default)
        {
            return _context.PaymentOrders
                .FirstOrDefaultAsync(
                    order => order.UserId == userId && order.IdempotencyKey == idempotencyKey,
                    cancellationToken);
        }

        public Task<PaymentOrder?> FindByRazorpayOrderIdAsync(
            string razorpayOrderId,
            CancellationToken cancellationToken = default)
        {
            return _context.PaymentOrders
                .FirstOrDefaultAsync(order => order.RazorpayOrderId == razorpayOrderId, cancellationToken);
        }

        public Task<bool> UserExistsAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.Users.AsNoTracking().AnyAsync(user => user.Id == userId, cancellationToken);
        }

        public async Task<(Guid PlanId, DateTime? ActivatedAt, DateTime? ExpiresAt)?> GetUserPlanProjectionAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            var row = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new { user.PlanId, user.PlanActivatedAt, user.PlanExpiresAt })
                .FirstOrDefaultAsync(cancellationToken);

            return row is null ? null : (row.PlanId, row.PlanActivatedAt, row.PlanExpiresAt);
        }

        public async Task<bool> OwnsCardAsync(
            Guid userId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            return contentType switch
            {
                ShareContentType.Poll => await _context.Polls
                    .AsNoTracking()
                    .AnyAsync(poll => poll.Id == contentId && poll.CreatorId == userId, cancellationToken),
                ShareContentType.Check => await _context.UserChecks
                    .AsNoTracking()
                    .AnyAsync(check => check.Id == contentId && check.UserId == userId, cancellationToken),
                _ => false
            };
        }

        public Task<bool> HasPermanentUnlockAsync(
            Guid userId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken = default)
        {
            return _context.CardUnlocks
                .AsNoTracking()
                .AnyAsync(
                    unlock => unlock.OwnerUserId == userId
                              && unlock.ContentType == contentType
                              && unlock.ContentId == contentId
                              && unlock.Source == CardUnlockSource.Purchase,
                    cancellationToken);
        }

        public async Task<PaymentOrder?> TryAddAsync(PaymentOrder order, CancellationToken cancellationToken = default)
        {
            _context.PaymentOrders.Add(order);
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
                return order;
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, IdempotencyConstraint))
            {
                _context.Entry(order).State = EntityState.Detached;
                return null;
            }
        }

        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres
                   && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                   && postgres.ConstraintName == constraintName;
        }
    }
}
