using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public sealed class PaymentFulfillmentRepository : IPaymentFulfillmentRepository
    {
        private const string PaymentIdConstraint = "IX_payment_attempts_razorpay_payment_id";
        private const string EntitlementOrderConstraint = "IX_user_plan_entitlements_payment_order_id";
        private const string CardUnlockConstraint = "IX_card_unlocks_owner_card";

        private readonly YakkuDbContext _context;

        public PaymentFulfillmentRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<PaymentOrder?> FindOrderForUpdateAsync(Guid orderId, CancellationToken cancellationToken = default)
        {
            return _context.PaymentOrders.FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
        }

        public Task<PaymentAttempt?> FindAttemptByRazorpayPaymentIdAsync(
            string razorpayPaymentId,
            CancellationToken cancellationToken = default)
        {
            return _context.PaymentAttempts.FirstOrDefaultAsync(
                attempt => attempt.RazorpayPaymentId == razorpayPaymentId,
                cancellationToken);
        }

        public async Task<PaymentFulfillmentResult> FulfillCapturedAsync(
            CapturedPaymentFulfillmentCommand command,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

            var lockKey = $"payment-fulfill:{command.PaymentOrderId:N}";
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock(hashtextextended({lockKey}, 0))",
                cancellationToken);

            var order = await _context.PaymentOrders
                .FirstOrDefaultAsync(item => item.Id == command.PaymentOrderId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Payment order not found.");

            if (!string.Equals(order.RazorpayOrderId, command.RazorpayOrderId, StringComparison.Ordinal))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.PaymentOrderMismatch,
                    "Payment does not belong to this order.");
            }

            if (command.AmountInPaise != order.AmountInPaise
                || !string.Equals(command.Currency, order.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.PaymentAmountMismatch,
                    "Payment amount or currency does not match the order.");
            }

            var existingAttempt = await _context.PaymentAttempts.FirstOrDefaultAsync(
                attempt => attempt.RazorpayPaymentId == command.RazorpayPaymentId,
                cancellationToken);

            if (order.Status == PaymentOrderStatus.Paid
                && existingAttempt is { Status: PaymentAttemptStatus.Captured })
            {
                var already = await BuildResultAsync(order, existingAttempt, alreadyFulfilled: true, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return already;
            }

            if (existingAttempt is { Status: PaymentAttemptStatus.Captured }
                && existingAttempt.PaymentOrderId != order.Id)
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This Razorpay payment was already applied to another order.");
            }

            var attempt = existingAttempt;
            if (attempt is null)
            {
                attempt = PaymentAttempt.Create(
                    order.Id,
                    order.AmountInPaise,
                    order.Currency,
                    command.CapturedAtUtc,
                    command.RazorpayPaymentId,
                    command.PaymentMethod,
                    command.RazorpayStatus);
                _context.PaymentAttempts.Add(attempt);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception, PaymentIdConstraint))
                {
                    _context.Entry(attempt).State = EntityState.Detached;
                    attempt = await _context.PaymentAttempts.FirstAsync(
                        item => item.RazorpayPaymentId == command.RazorpayPaymentId,
                        cancellationToken);
                    if (attempt.Status == PaymentAttemptStatus.Captured)
                    {
                        var raced = await BuildResultAsync(order, attempt, alreadyFulfilled: true, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        return raced;
                    }
                }
            }

            if (attempt.Status != PaymentAttemptStatus.Captured)
            {
                attempt.MarkCaptured(command.CapturedAtUtc, command.RazorpayStatus, command.PaymentMethod);
            }

            if (order.Status != PaymentOrderStatus.Paid)
            {
                order.MarkPaid(command.CapturedAtUtc);
            }

            await ApplyEntitlementAsync(order, command.CapturedAtUtc, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return await BuildResultAsync(order, attempt, alreadyFulfilled: false, cancellationToken);
        }

        private async Task ApplyEntitlementAsync(
            PaymentOrder order,
            DateTime capturedAt,
            CancellationToken cancellationToken)
        {
            if (order.PurchaseType == PaymentPurchaseType.UserPlan)
            {
                await ActivateUserPlanAsync(order, capturedAt, cancellationToken);
                return;
            }

            if (order.PurchaseType == PaymentPurchaseType.ConversationTopUp)
            {
                await ApplyConversationTopUpAsync(order, capturedAt, cancellationToken);
                return;
            }

            await ActivateCardUnlockAsync(order, capturedAt, cancellationToken);
        }

        private async Task ActivateUserPlanAsync(
            PaymentOrder order,
            DateTime capturedAt,
            CancellationToken cancellationToken)
        {
            var existingForOrder = await _context.UserPlanEntitlements.FirstOrDefaultAsync(
                item => item.PaymentOrderId == order.Id,
                cancellationToken);
            if (existingForOrder is not null)
            {
                return;
            }

            var planId = order.PlanId
                ?? throw new AppException(500, ApiErrorCodes.InternalServerError, "Payment order is missing a plan.");
            var plan = await _context.Plans.FirstOrDefaultAsync(item => item.Id == planId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Plan not found.");

            if (plan.DurationDays is not { } days || days < 1)
            {
                throw new AppException(
                    500,
                    ApiErrorCodes.InternalServerError,
                    "Purchased plan is missing a duration.");
            }

            var user = await _context.Users
                    .Include(item => item.UserPlan)
                    .FirstOrDefaultAsync(item => item.Id == order.UserId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "User not found.");

            var active = await _context.UserPlanEntitlements
                .Where(item => item.UserId == order.UserId && item.Status == UserPlanEntitlementStatus.Active)
                .ToListAsync(cancellationToken);

            foreach (var entitlement in active)
            {
                // Love supersedes Lite; any other active paid row is superseded when a new paid period starts.
                entitlement.MarkSuperseded(capturedAt);
            }

            var created = UserPlanEntitlement.Activate(
                order.UserId,
                plan.Id,
                capturedAt,
                capturedAt.AddDays(days),
                order.Id);
            _context.UserPlanEntitlements.Add(created);

            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (IsUniqueViolation(exception, EntitlementOrderConstraint))
            {
                _context.Entry(created).State = EntityState.Detached;
                // Another worker created it; user projection may still need updating below.
            }

            // Buying Lite or Love gives the asker a fresh question budget on every conversation they started, without
            // touching their unlocked cards. Blocked chats stay as they are; top-up credits are kept.
            await AskBackBudgetReset.ForPlanPurchaseAsync(
                _context, order.UserId, plan.AskBackMessageLimit, cancellationToken);

            user.ActivatePlan(plan, capturedAt);
        }

        /// <summary>
        /// Adds the top-up's questions to its conversation, once per payment order. The grant row and the counter
        /// update run in the fulfillment transaction, so a retry cannot grant twice.
        /// </summary>
        private async Task ApplyConversationTopUpAsync(
            PaymentOrder order,
            DateTime capturedAt,
            CancellationToken cancellationToken)
        {
            if (order.ConversationId is not { } conversationId)
            {
                throw new AppException(
                    500,
                    ApiErrorCodes.InternalServerError,
                    "Top-up payment order is missing its conversation.");
            }

            var alreadyGranted = await _context.AskBackConversationTopUps.AnyAsync(
                item => item.PaymentOrderId == order.Id,
                cancellationToken);
            if (alreadyGranted)
            {
                return;
            }

            var planId = order.PlanId
                ?? throw new AppException(500, ApiErrorCodes.InternalServerError, "Payment order is missing a plan.");
            var credits = await _context.Plans.AsNoTracking()
                .Where(plan => plan.Id == planId)
                .Select(plan => plan.AskBackMessageLimit)
                .FirstOrDefaultAsync(cancellationToken);
            if (credits is not { } count || count < 1)
            {
                throw new AppException(
                    500,
                    ApiErrorCodes.InternalServerError,
                    "Top-up plan is missing its question count.");
            }

            var conversationExists = await _context.AskBackConversations.AnyAsync(
                conversation => conversation.Id == conversationId,
                cancellationToken);
            if (!conversationExists)
            {
                // Nothing to grant to: the fulfillment result reports no credits and the service logs it.
                return;
            }

            _context.AskBackConversationTopUps.Add(
                AskBackConversationTopUp.Create(conversationId, order.Id, count, capturedAt));
            await _context.SaveChangesAsync(cancellationToken);

            await _context.AskBackConversations
                .Where(conversation => conversation.Id == conversationId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        conversation => conversation.TopUpGranted,
                        conversation => conversation.TopUpGranted + count),
                    cancellationToken);
        }

        private async Task ActivateCardUnlockAsync(
            PaymentOrder order,
            DateTime capturedAt,
            CancellationToken cancellationToken)
        {
            if (order.ContentType is not { } contentType || order.ContentId is not { } contentId)
            {
                throw new AppException(
                    500,
                    ApiErrorCodes.InternalServerError,
                    "Card unlock payment order is missing card details.");
            }

            var existing = await _context.CardUnlocks.FirstOrDefaultAsync(
                item => item.OwnerUserId == order.UserId
                        && item.ContentType == contentType
                        && item.ContentId == contentId,
                cancellationToken);

            if (existing is null)
            {
                var unlock = CardUnlock.Create(
                    order.UserId,
                    contentType,
                    contentId,
                    CardUnlockSource.Purchase,
                    capturedAt,
                    paymentOrderId: order.Id);
                _context.CardUnlocks.Add(unlock);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception, CardUnlockConstraint))
                {
                    _context.Entry(unlock).State = EntityState.Detached;
                    existing = await _context.CardUnlocks.FirstAsync(
                        item => item.OwnerUserId == order.UserId
                                && item.ContentType == contentType
                                && item.ContentId == contentId,
                        cancellationToken);
                }
            }

            if (existing is not null)
            {
                existing.PromoteToPurchase(order.Id);
            }
        }

        public async Task RecordFailedAttemptAsync(
            Guid paymentOrderId,
            string razorpayPaymentId,
            int amountInPaise,
            string currency,
            string? paymentMethod,
            string? razorpayStatus,
            string? failureCode,
            string? failureReason,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            var existing = await _context.PaymentAttempts.FirstOrDefaultAsync(
                attempt => attempt.RazorpayPaymentId == razorpayPaymentId,
                cancellationToken);

            if (existing is { Status: PaymentAttemptStatus.Captured })
            {
                return;
            }

            if (existing is null)
            {
                var attempt = PaymentAttempt.Create(
                    paymentOrderId,
                    amountInPaise,
                    currency,
                    utcNow,
                    razorpayPaymentId,
                    paymentMethod,
                    razorpayStatus);
                attempt.MarkFailed(utcNow, failureCode, failureReason, razorpayStatus);
                _context.PaymentAttempts.Add(attempt);
                try
                {
                    await _context.SaveChangesAsync(cancellationToken);
                }
                catch (DbUpdateException exception) when (IsUniqueViolation(exception, PaymentIdConstraint))
                {
                    _context.Entry(attempt).State = EntityState.Detached;
                }

                return;
            }

            if (existing.Status != PaymentAttemptStatus.Failed)
            {
                existing.MarkFailed(utcNow, failureCode, failureReason, razorpayStatus);
                await _context.SaveChangesAsync(cancellationToken);
            }
        }

        private async Task<PaymentFulfillmentResult> BuildResultAsync(
            PaymentOrder order,
            PaymentAttempt attempt,
            bool alreadyFulfilled,
            CancellationToken cancellationToken)
        {
            string? planCode = null;
            DateTime? expiresAt = null;
            if (order.PlanId is { } planId)
            {
                planCode = await _context.Plans.AsNoTracking()
                    .Where(plan => plan.Id == planId)
                    .Select(plan => plan.Code)
                    .FirstOrDefaultAsync(cancellationToken);

                if (order.PurchaseType == PaymentPurchaseType.UserPlan)
                {
                    expiresAt = await _context.UserPlans.AsNoTracking()
                        .Where(userPlan => userPlan.UserId == order.UserId)
                        .Select(userPlan => userPlan.PlanExpiresAt)
                        .FirstOrDefaultAsync(cancellationToken);
                }
            }

            Guid? conversationId = null;
            int? topUpCredits = null;
            string? conversationStatus = null;
            if (order.PurchaseType == PaymentPurchaseType.ConversationTopUp && order.ConversationId is { } orderConversationId)
            {
                conversationId = orderConversationId;
                topUpCredits = await _context.AskBackConversationTopUps.AsNoTracking()
                    .Where(item => item.PaymentOrderId == order.Id)
                    .Select(item => (int?)item.Credits)
                    .FirstOrDefaultAsync(cancellationToken);
                var status = await _context.AskBackConversations.AsNoTracking()
                    .Where(conversation => conversation.Id == orderConversationId)
                    .Select(conversation => (AskBackStatus?)conversation.Status)
                    .FirstOrDefaultAsync(cancellationToken);
                conversationStatus = status?.ToString().ToUpperInvariant();
            }

            return new PaymentFulfillmentResult(
                alreadyFulfilled,
                order.Id,
                attempt.Id,
                order.PurchaseType,
                planCode,
                expiresAt,
                order.ContentType,
                order.ContentId,
                order.UserId,
                conversationId,
                topUpCredits,
                conversationStatus);
        }

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres
                   && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                   && postgres.ConstraintName == constraintName;
        }
    }
}
