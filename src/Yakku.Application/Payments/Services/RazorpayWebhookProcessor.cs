using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Services
{
    public sealed class RazorpayWebhookProcessor : IRazorpayWebhookProcessor
    {
        private readonly IRazorpayWebhookInbox _inbox;
        private readonly IPaymentOrderRepository _orders;
        private readonly IPaymentFulfillmentService _fulfillment;
        private readonly IPaymentFulfillmentRepository _fulfillmentRepository;
        private readonly IPaymentLogWriter _paymentLogs;
        private readonly TimeProvider _time;
        private readonly ILogger<RazorpayWebhookProcessor> _logger;

        public RazorpayWebhookProcessor(
            IRazorpayWebhookInbox inbox,
            IPaymentOrderRepository orders,
            IPaymentFulfillmentService fulfillment,
            IPaymentFulfillmentRepository fulfillmentRepository,
            IPaymentLogWriter paymentLogs,
            TimeProvider time,
            ILogger<RazorpayWebhookProcessor> logger)
        {
            _inbox = inbox;
            _orders = orders;
            _fulfillment = fulfillment;
            _fulfillmentRepository = fulfillmentRepository;
            _paymentLogs = paymentLogs;
            _time = time;
            _logger = logger;
        }

        public async Task ProcessAsync(Guid webhookEventId, CancellationToken cancellationToken = default)
        {
            // Claimed rows are already Processing; load via claim id through inbox mark helpers after work.
            // The worker passes the claimed entity id; re-load payload from inbox claim is done in repository.
            await ProcessClaimedAsync(webhookEventId, cancellationToken);
        }

        internal async Task ProcessClaimedAsync(Guid webhookEventId, CancellationToken cancellationToken)
        {
            var now = _time.GetUtcNow().UtcDateTime;
            try
            {
                var payload = await _inbox.GetPayloadAsync(webhookEventId, cancellationToken);
                if (payload is null)
                {
                    return;
                }

                using var document = JsonDocument.Parse(payload.Payload);
                var eventType = payload.EventType;

                switch (eventType)
                {
                    case "payment.captured":
                    case "order.paid":
                        await HandleCapturedAsync(document.RootElement, eventType, webhookEventId, now, cancellationToken);
                        break;

                    case "payment.authorized":
                        await _paymentLogs.WriteAsync(
                            new PaymentLogEntry(
                                PaymentLogEventTypes.PaymentAuthorized,
                                PaymentLogSources.Webhook,
                                PaymentLogStatus.Information,
                                "payment.authorized received; waiting for capture before granting access.",
                                CorrelationId: webhookEventId.ToString()),
                            cancellationToken);
                        break;

                    case "payment.failed":
                        await HandleFailedAsync(document.RootElement, webhookEventId, now, cancellationToken);
                        break;

                    default:
                        _logger.LogInformation("Ignoring Razorpay webhook event type {EventType}.", eventType);
                        break;
                }

                await _inbox.MarkProcessedAsync(webhookEventId, now, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Webhook processing failed for {WebhookEventId} ({ExceptionType}).",
                    webhookEventId,
                    exception.GetType().Name);

                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.WebhookProcessingFailed,
                        PaymentLogSources.Webhook,
                        PaymentLogStatus.Failure,
                        "Webhook processing failed.",
                        CorrelationId: webhookEventId.ToString(),
                        Metadata: new Dictionary<string, string?>
                        {
                            ["exceptionType"] = exception.GetType().Name
                        }),
                    cancellationToken);

                await _inbox.MarkFailedAsync(webhookEventId, exception.GetType().Name, cancellationToken);
            }
        }

        private async Task HandleCapturedAsync(
            JsonElement root,
            string eventType,
            Guid webhookEventId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (!TryReadPaymentEntity(root, out var paymentId, out var orderId, out var amount, out var currency, out var status, out var method, out var captured))
            {
                throw new InvalidOperationException($"Unable to read payment entity from {eventType}.");
            }

            if (!captured && !string.Equals(status, "captured", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Skipping non-captured payment webhook. PaymentId present, Status {Status}.",
                    status);
                return;
            }

            var order = await _orders.FindByRazorpayOrderIdAsync(orderId, cancellationToken);
            if (order is null)
            {
                // Out-of-order / unknown order: keep the inbox retryable until the local order exists or retries exhaust.
                throw new InvalidOperationException("Local payment order not found for Razorpay order.");
            }

            // Out-of-order safety: never downgrade a captured fulfillment.
            var existing = await _fulfillmentRepository.FindAttemptByRazorpayPaymentIdAsync(paymentId, cancellationToken);
            if (existing is { Status: PaymentAttemptStatus.Captured } || order.Status == PaymentOrderStatus.Paid)
            {
                await _fulfillment.FulfillCapturedPaymentAsync(
                    new CapturedPaymentFulfillmentCommand(
                        order.Id,
                        orderId,
                        paymentId,
                        order.AmountInPaise,
                        order.Currency,
                        method ?? existing?.PaymentMethod,
                        status ?? "captured",
                        existing?.CapturedAt ?? now,
                        webhookEventId.ToString()),
                    cancellationToken);
                return;
            }

            if (amount != order.AmountInPaise
                || !string.Equals(currency, order.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Webhook payment amount/currency mismatch.");
            }

            await _fulfillment.FulfillCapturedPaymentAsync(
                new CapturedPaymentFulfillmentCommand(
                    order.Id,
                    orderId,
                    paymentId,
                    order.AmountInPaise,
                    order.Currency,
                    method,
                    status ?? "captured",
                    now,
                    webhookEventId.ToString()),
                cancellationToken);
        }

        private async Task HandleFailedAsync(
            JsonElement root,
            Guid webhookEventId,
            DateTime now,
            CancellationToken cancellationToken)
        {
            if (!TryReadPaymentEntity(root, out var paymentId, out var orderId, out var amount, out var currency, out var status, out var method, out _))
            {
                throw new InvalidOperationException("Unable to read payment entity from payment.failed.");
            }

            var order = await _orders.FindByRazorpayOrderIdAsync(orderId, cancellationToken);
            if (order is null)
            {
                _logger.LogInformation("payment.failed for unknown local order; acknowledging.");
                return;
            }

            var existing = await _fulfillmentRepository.FindAttemptByRazorpayPaymentIdAsync(paymentId, cancellationToken);
            if (existing is { Status: PaymentAttemptStatus.Captured } || order.Status == PaymentOrderStatus.Paid)
            {
                // Out-of-order failed after capture: do not downgrade.
                _logger.LogInformation("Ignoring payment.failed after capture for order {OrderId}.", order.Id);
                return;
            }

            await _fulfillmentRepository.RecordFailedAttemptAsync(
                order.Id,
                paymentId,
                amount > 0 ? amount : order.AmountInPaise,
                string.IsNullOrWhiteSpace(currency) ? order.Currency : currency,
                method,
                status ?? "failed",
                failureCode: "webhook_payment_failed",
                failureReason: "payment.failed webhook",
                now,
                cancellationToken);

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.PaymentFailed,
                    PaymentLogSources.Webhook,
                    PaymentLogStatus.Failure,
                    "payment.failed webhook recorded.",
                    PaymentOrderId: order.Id,
                    CorrelationId: webhookEventId.ToString(),
                    RazorpayOrderId: orderId,
                    RazorpayPaymentId: paymentId),
                cancellationToken);
        }

        private static bool TryReadPaymentEntity(
            JsonElement root,
            out string paymentId,
            out string orderId,
            out int amount,
            out string currency,
            out string? status,
            out string? method,
            out bool captured)
        {
            paymentId = string.Empty;
            orderId = string.Empty;
            amount = 0;
            currency = string.Empty;
            status = null;
            method = null;
            captured = false;

            if (!root.TryGetProperty("payload", out var payload))
            {
                return false;
            }

            JsonElement entity;
            if (payload.TryGetProperty("payment", out var payment)
                && payment.TryGetProperty("entity", out entity))
            {
                // payment.* events
            }
            else if (payload.TryGetProperty("order", out _)
                     && payload.TryGetProperty("payment", out payment)
                     && payment.TryGetProperty("entity", out entity))
            {
                // order.paid
            }
            else
            {
                return false;
            }

            paymentId = entity.TryGetProperty("id", out var idNode) ? idNode.GetString()?.Trim() ?? string.Empty : string.Empty;
            orderId = entity.TryGetProperty("order_id", out var orderNode) ? orderNode.GetString()?.Trim() ?? string.Empty : string.Empty;
            amount = entity.TryGetProperty("amount", out var amountNode) && amountNode.TryGetInt32(out var a) ? a : 0;
            currency = entity.TryGetProperty("currency", out var currencyNode)
                ? currencyNode.GetString()?.Trim().ToUpperInvariant() ?? string.Empty
                : string.Empty;
            status = entity.TryGetProperty("status", out var statusNode) ? statusNode.GetString() : null;
            method = entity.TryGetProperty("method", out var methodNode) ? methodNode.GetString() : null;
            captured = entity.TryGetProperty("captured", out var capturedNode) && capturedNode.ValueKind == JsonValueKind.True;

            return paymentId.Length > 0 && orderId.Length > 0;
        }
    }
}
