using Microsoft.Extensions.Logging;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Services
{
    /// <summary>
    /// Idempotent reconciliation: expire stale local orders, fulfill gateway-captured payments that never reached
    /// verify/webhook, and audit remaining mismatches. Never grants access from local status alone.
    /// </summary>
    public sealed class PaymentReconciliationService : IPaymentReconciliationService
    {
        public const int BatchSize = 50;

        private readonly IPaymentReconciliationRepository _repository;
        private readonly IRazorpayClient _razorpay;
        private readonly IPaymentFulfillmentService _fulfillment;
        private readonly IPaymentLogWriter _paymentLogs;
        private readonly TimeProvider _time;
        private readonly ILogger<PaymentReconciliationService> _logger;

        public PaymentReconciliationService(
            IPaymentReconciliationRepository repository,
            IRazorpayClient razorpay,
            IPaymentFulfillmentService fulfillment,
            IPaymentLogWriter paymentLogs,
            TimeProvider time,
            ILogger<PaymentReconciliationService> logger)
        {
            _repository = repository;
            _razorpay = razorpay;
            _fulfillment = fulfillment;
            _paymentLogs = paymentLogs;
            _time = time;
            _logger = logger;
        }

        public async Task<PaymentReconciliationRunResult> RunAsync(CancellationToken cancellationToken = default)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            var expired = await ExpireLocalOrdersAsync(now, cancellationToken);
            var fulfilled = await FulfillCapturedGatewayPaymentsAsync(now, cancellationToken);
            var mismatches = await LogMissingFulfillmentAsync(cancellationToken);
            var webhooks = await LogExhaustedWebhooksAsync(cancellationToken);

            _logger.LogInformation(
                "Payment reconciliation finished. Expired {Expired}, Fulfilled {Fulfilled}, Mismatches {Mismatches}, ExhaustedWebhooks {Webhooks}.",
                expired,
                fulfilled,
                mismatches,
                webhooks);

            return new PaymentReconciliationRunResult(expired, fulfilled, mismatches, webhooks);
        }

        private async Task<int> ExpireLocalOrdersAsync(DateTime now, CancellationToken cancellationToken)
        {
            var orders = await _repository.ListExpiredOpenOrdersAsync(now, BatchSize, cancellationToken);
            foreach (var order in orders)
            {
                await _repository.MarkOrderExpiredAsync(order.Id, now, cancellationToken);
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.PaymentOrderExpired,
                        PaymentLogSources.Worker,
                        PaymentLogStatus.Information,
                        "Unpaid payment order marked expired by reconciliation.",
                        PaymentOrderId: order.Id,
                        RazorpayOrderId: order.RazorpayOrderId,
                        CorrelationId: order.CorrelationId),
                    cancellationToken);
            }

            return orders.Count;
        }

        private async Task<int> FulfillCapturedGatewayPaymentsAsync(DateTime now, CancellationToken cancellationToken)
        {
            if (!_razorpay.IsEnabled)
            {
                return 0;
            }

            var pending = await _repository.ListPendingOrdersWithGatewayIdAsync(now, BatchSize, cancellationToken);
            var fulfilled = 0;
            foreach (var order in pending)
            {
                if (string.IsNullOrWhiteSpace(order.RazorpayOrderId))
                {
                    continue;
                }

                var payments = await _razorpay.GetOrderPaymentsAsync(order.RazorpayOrderId, cancellationToken);
                var captured = payments.FirstOrDefault(payment =>
                    payment.Succeeded
                    && payment.Captured
                    && !string.IsNullOrWhiteSpace(payment.RazorpayPaymentId));

                if (captured is null)
                {
                    continue;
                }

                if (captured.AmountInPaise is not { } amount
                    || !string.Equals(captured.Currency, order.Currency, StringComparison.OrdinalIgnoreCase)
                    || amount != order.AmountInPaise)
                {
                    await _paymentLogs.WriteAsync(
                        new PaymentLogEntry(
                            PaymentLogEventTypes.PaymentReconciliationMismatch,
                            PaymentLogSources.Worker,
                            PaymentLogStatus.Warning,
                            "Gateway captured payment amount/currency does not match local order; not fulfilling.",
                            PaymentOrderId: order.Id,
                            RazorpayOrderId: order.RazorpayOrderId,
                            RazorpayPaymentId: captured.RazorpayPaymentId,
                            CorrelationId: order.CorrelationId),
                        cancellationToken);
                    continue;
                }

                await _fulfillment.FulfillCapturedPaymentAsync(
                    new CapturedPaymentFulfillmentCommand(
                        order.Id,
                        order.RazorpayOrderId,
                        captured.RazorpayPaymentId!,
                        order.AmountInPaise,
                        order.Currency,
                        captured.Method,
                        captured.Status ?? "captured",
                        now,
                        $"reconcile:{order.Id:N}"),
                    cancellationToken);
                fulfilled++;
            }

            return fulfilled;
        }

        private async Task<int> LogMissingFulfillmentAsync(CancellationToken cancellationToken)
        {
            var missing = await _repository.ListPaidOrdersMissingFulfillmentAsync(BatchSize, cancellationToken);
            foreach (var order in missing)
            {
                var attempt = await _repository.FindCapturedAttemptAsync(order.Id, cancellationToken);
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.PaymentReconciliationMismatch,
                        PaymentLogSources.Worker,
                        PaymentLogStatus.Warning,
                        "Paid order is missing plan entitlement or permanent unlock.",
                        PaymentOrderId: order.Id,
                        PaymentAttemptId: attempt?.Id,
                        RazorpayOrderId: order.RazorpayOrderId,
                        RazorpayPaymentId: attempt?.RazorpayPaymentId,
                        CorrelationId: order.CorrelationId,
                        Metadata: new Dictionary<string, string?>
                        {
                            ["purchaseType"] = order.PurchaseType.ToString()
                        }),
                    cancellationToken);
            }

            return missing.Count;
        }

        private async Task<int> LogExhaustedWebhooksAsync(CancellationToken cancellationToken)
        {
            var exhausted = await _repository.ListExhaustedFailedWebhooksAsync(
                maxRetries: 8,
                BatchSize,
                cancellationToken);

            foreach (var item in exhausted)
            {
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.WebhookProcessingFailed,
                        PaymentLogSources.Worker,
                        PaymentLogStatus.Failure,
                        "Webhook retries exhausted; needs manual review.",
                        CorrelationId: item.Id.ToString(),
                        Metadata: new Dictionary<string, string?>
                        {
                            ["eventType"] = item.EventType,
                            ["razorpayEventId"] = item.RazorpayEventId,
                            ["retryCount"] = item.RetryCount.ToString(),
                            ["error"] = item.ErrorMessage
                        }),
                    cancellationToken);
            }

            return exhausted.Count;
        }
    }
}
