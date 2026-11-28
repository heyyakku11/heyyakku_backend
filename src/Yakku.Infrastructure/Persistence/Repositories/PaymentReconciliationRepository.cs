using Microsoft.EntityFrameworkCore;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public sealed class PaymentReconciliationRepository : IPaymentReconciliationRepository
    {
        private readonly YakkuDbContext _context;

        public PaymentReconciliationRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<PaymentOrder>> ListExpiredOpenOrdersAsync(
            DateTime utcNow,
            int take,
            CancellationToken cancellationToken = default)
        {
            return await _context.PaymentOrders
                .Where(order =>
                    (order.Status == PaymentOrderStatus.Created || order.Status == PaymentOrderStatus.Pending)
                    && order.ExpiresAt <= utcNow)
                .OrderBy(order => order.ExpiresAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task MarkOrderExpiredAsync(Guid orderId, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var order = await _context.PaymentOrders.FirstOrDefaultAsync(item => item.Id == orderId, cancellationToken);
            if (order is null
                || order.Status is PaymentOrderStatus.Paid or PaymentOrderStatus.Expired or PaymentOrderStatus.Cancelled)
            {
                return;
            }

            order.MarkExpired(utcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PaymentOrder>> ListPendingOrdersWithGatewayIdAsync(
            DateTime utcNow,
            int take,
            CancellationToken cancellationToken = default)
        {
            // Include recently expired pending orders so a late capture can still be fulfilled once.
            var floor = utcNow.AddHours(-24);
            return await _context.PaymentOrders.AsNoTracking()
                .Where(order =>
                    order.RazorpayOrderId != null
                    && ((order.Status == PaymentOrderStatus.Pending && order.ExpiresAt > utcNow)
                        || (order.Status == PaymentOrderStatus.Expired && order.ExpiresAt >= floor)))
                .OrderBy(order => order.CreatedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public async Task<IReadOnlyList<PaymentOrder>> ListPaidOrdersMissingFulfillmentAsync(
            int take,
            CancellationToken cancellationToken = default)
        {
            var paid = await _context.PaymentOrders.AsNoTracking()
                .Where(order => order.Status == PaymentOrderStatus.Paid)
                .OrderByDescending(order => order.PaidAt)
                .Take(take * 3)
                .ToListAsync(cancellationToken);

            var missing = new List<PaymentOrder>();
            foreach (var order in paid)
            {
                if (order.PurchaseType == PaymentPurchaseType.UserPlan)
                {
                    var hasEntitlement = await _context.UserPlanEntitlements.AsNoTracking()
                        .AnyAsync(item => item.PaymentOrderId == order.Id, cancellationToken);
                    if (!hasEntitlement)
                    {
                        missing.Add(order);
                    }
                }
                else if (order.PurchaseType == PaymentPurchaseType.ConversationTopUp)
                {
                    var hasGrant = await _context.AskBackConversationTopUps.AsNoTracking()
                        .AnyAsync(item => item.PaymentOrderId == order.Id, cancellationToken);
                    if (!hasGrant)
                    {
                        missing.Add(order);
                    }
                }
                else if (order.ContentType is { } contentType && order.ContentId is { } contentId)
                {
                    var hasUnlock = await _context.CardUnlocks.AsNoTracking()
                        .AnyAsync(
                            item => item.OwnerUserId == order.UserId
                                    && item.ContentType == contentType
                                    && item.ContentId == contentId
                                    && item.Source == CardUnlockSource.Purchase,
                            cancellationToken);
                    if (!hasUnlock)
                    {
                        missing.Add(order);
                    }
                }

                if (missing.Count >= take)
                {
                    break;
                }
            }

            return missing;
        }

        public async Task<IReadOnlyList<WebhookEvent>> ListExhaustedFailedWebhooksAsync(
            int maxRetries,
            int take,
            CancellationToken cancellationToken = default)
        {
            return await _context.WebhookEvents.AsNoTracking()
                .Where(item =>
                    item.ProcessingStatus == WebhookProcessingStatus.Failed
                    && item.RetryCount >= maxRetries)
                .OrderByDescending(item => item.ReceivedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }

        public Task<PaymentAttempt?> FindCapturedAttemptAsync(
            Guid orderId,
            CancellationToken cancellationToken = default)
        {
            return _context.PaymentAttempts.AsNoTracking()
                .Where(attempt =>
                    attempt.PaymentOrderId == orderId && attempt.Status == PaymentAttemptStatus.Captured)
                .OrderByDescending(attempt => attempt.CapturedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }
    }
}
