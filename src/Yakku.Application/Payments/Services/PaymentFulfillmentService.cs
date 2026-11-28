using Yakku.Application.Caching;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Services
{
    /// <summary>
    /// Thin application wrapper over the transactional fulfillment repository. It writes the audit trail for what the
    /// repository did and clears the caches that the purchase changes.
    /// </summary>
    public sealed class PaymentFulfillmentService : IPaymentFulfillmentService
    {
        private readonly IPaymentFulfillmentRepository _repository;
        private readonly IPaymentLogWriter _paymentLogs;
        private readonly IResponseCache _cache;

        public PaymentFulfillmentService(
            IPaymentFulfillmentRepository repository,
            IPaymentLogWriter paymentLogs,
            IResponseCache? cache = null)
        {
            _repository = repository;
            _paymentLogs = paymentLogs;
            _cache = cache ?? new NullResponseCache();
        }

        public async Task<PaymentFulfillmentResult> FulfillCapturedPaymentAsync(
            CapturedPaymentFulfillmentCommand command,
            CancellationToken cancellationToken = default)
        {
            var result = await _repository.FulfillCapturedAsync(command, cancellationToken);

            if (!result.AlreadyFulfilled)
            {
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.PaymentCaptured,
                        PaymentLogSources.Api,
                        PaymentLogStatus.Success,
                        "Captured payment fulfilled.",
                        PaymentOrderId: result.PaymentOrderId,
                        PaymentAttemptId: result.PaymentAttemptId,
                        CorrelationId: command.CorrelationId,
                        RazorpayOrderId: command.RazorpayOrderId,
                        RazorpayPaymentId: command.RazorpayPaymentId,
                        Metadata: new Dictionary<string, string?>
                        {
                            ["purchaseType"] = result.PurchaseType.ToString(),
                            ["planCode"] = result.PlanCode
                        }),
                    cancellationToken);

                switch (result.PurchaseType)
                {
                    case PaymentPurchaseType.UserPlan:
                        await LogEntitlementAsync(result, command, cancellationToken);
                        break;
                    case PaymentPurchaseType.CardUnlock:
                        await LogCardUnlockAsync(result, command, cancellationToken);
                        break;
                    case PaymentPurchaseType.ConversationTopUp:
                        await LogConversationTopUpAsync(result, command, cancellationToken);
                        break;
                }

                // Lite and Love reset the asker budget on the buyer's chats, and a top-up changes one chat's count.
                if (result.PurchaseType is PaymentPurchaseType.UserPlan or PaymentPurchaseType.ConversationTopUp
                    && result.UserId != Guid.Empty)
                {
                    await _cache.RemoveManyAsync(
                        ResponseCacheKeys.AfterAskBackListChange(result.UserId),
                        cancellationToken);
                }
            }

            return result;
        }

        private async Task LogEntitlementAsync(
            PaymentFulfillmentResult result,
            CapturedPaymentFulfillmentCommand command,
            CancellationToken cancellationToken)
        {
            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.EntitlementCreated,
                    PaymentLogSources.System,
                    PaymentLogStatus.Success,
                    "User plan entitlement activated.",
                    PaymentOrderId: result.PaymentOrderId,
                    CorrelationId: command.CorrelationId,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["planCode"] = result.PlanCode,
                        ["expiresAt"] = result.PlanExpiresAt?.ToString("O")
                    }),
                cancellationToken);

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.AskBackBudgetReset,
                    PaymentLogSources.System,
                    PaymentLogStatus.Information,
                    "Ask back budgets reset on the buyer's unlocked conversations.",
                    PaymentOrderId: result.PaymentOrderId,
                    UserId: result.UserId,
                    CorrelationId: command.CorrelationId,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["planCode"] = result.PlanCode
                    }),
                cancellationToken);
        }

        private async Task LogCardUnlockAsync(
            PaymentFulfillmentResult result,
            CapturedPaymentFulfillmentCommand command,
            CancellationToken cancellationToken)
        {
            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.CardUnlockCreated,
                    PaymentLogSources.System,
                    PaymentLogStatus.Success,
                    "Permanent card unlock created.",
                    PaymentOrderId: result.PaymentOrderId,
                    CorrelationId: command.CorrelationId,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["contentType"] = result.ContentType?.ToString(),
                        ["contentId"] = result.ContentId?.ToString()
                    }),
                cancellationToken);
        }

        private async Task LogConversationTopUpAsync(
            PaymentFulfillmentResult result,
            CapturedPaymentFulfillmentCommand command,
            CancellationToken cancellationToken)
        {
            // Nothing was granted: the order is paid but needs a refund or a manual grant. Reconciliation lists it too.
            if (result.TopUpCredits is null)
            {
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.ConversationTopUpNotApplied,
                        PaymentLogSources.System,
                        PaymentLogStatus.Failure,
                        "Conversation top-up paid but not applied: the conversation is missing or no longer the buyer's. Review for refund.",
                        PaymentOrderId: result.PaymentOrderId,
                        UserId: result.UserId,
                        CorrelationId: command.CorrelationId,
                        Metadata: new Dictionary<string, string?>
                        {
                            ["conversationId"] = result.ConversationId?.ToString()
                        }),
                    cancellationToken);
                return;
            }

            // Granted, but the chat is not active any more (blocked or closed): kept, and flagged for review.
            var active = string.Equals(result.ConversationStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase);
            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.ConversationTopUpGranted,
                    PaymentLogSources.System,
                    active ? PaymentLogStatus.Success : PaymentLogStatus.Warning,
                    active
                        ? "Conversation top-up granted."
                        : "Conversation top-up granted while the conversation is not active. Review.",
                    PaymentOrderId: result.PaymentOrderId,
                    UserId: result.UserId,
                    CorrelationId: command.CorrelationId,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["conversationId"] = result.ConversationId?.ToString(),
                        ["credits"] = result.TopUpCredits?.ToString(),
                        ["conversationStatus"] = result.ConversationStatus
                    }),
                cancellationToken);
        }
    }
}
