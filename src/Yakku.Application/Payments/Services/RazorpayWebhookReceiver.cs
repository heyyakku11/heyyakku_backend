using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Services
{
    public sealed class RazorpayWebhookReceiver : IRazorpayWebhookReceiver
    {
        private readonly IRazorpayWebhookInbox _inbox;
        private readonly IPaymentLogWriter _paymentLogs;
        private readonly RazorpayOptions _razorpayOptions;
        private readonly TimeProvider _time;
        private readonly ILogger<RazorpayWebhookReceiver> _logger;
        private readonly RazorpayWebhookSignal _signal;

        public RazorpayWebhookReceiver(
            IRazorpayWebhookInbox inbox,
            IPaymentLogWriter paymentLogs,
            RazorpayOptions razorpayOptions,
            TimeProvider time,
            ILogger<RazorpayWebhookReceiver> logger,
            RazorpayWebhookSignal signal)
        {
            _inbox = inbox;
            _paymentLogs = paymentLogs;
            _razorpayOptions = razorpayOptions;
            _time = time;
            _logger = logger;
            _signal = signal;
        }

        public async Task<RazorpayWebhookReceiveResult> ReceiveAsync(
            byte[] rawBody,
            string? signatureHeader,
            string? eventIdHeader,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            if (!_razorpayOptions.Enabled)
            {
                return new RazorpayWebhookReceiveResult(
                    false, false, ApiErrorCodes.PaymentProviderDisabled, "Payments are temporarily unavailable.", null);
            }

            if (rawBody.Length == 0)
            {
                return new RazorpayWebhookReceiveResult(
                    false, false, ApiErrorCodes.ValidationError, "Webhook body is required.", null);
            }

            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                return new RazorpayWebhookReceiveResult(
                    false, false, ApiErrorCodes.WebhookSignatureInvalid, "Missing X-Razorpay-Signature.", null);
            }

            if (!RazorpayWebhookSignature.IsValid(rawBody, signatureHeader, _razorpayOptions.WebhookSecret))
            {
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.WebhookSignatureVerificationFailed,
                        PaymentLogSources.Webhook,
                        PaymentLogStatus.Failure,
                        "Webhook signature verification failed.",
                        CorrelationId: correlationId),
                    cancellationToken);

                return new RazorpayWebhookReceiveResult(
                    false, false, ApiErrorCodes.WebhookSignatureInvalid, "Webhook signature is invalid.", null);
            }

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.WebhookSignatureVerified,
                    PaymentLogSources.Webhook,
                    PaymentLogStatus.Success,
                    "Webhook signature verified.",
                    CorrelationId: correlationId),
                cancellationToken);

            var rawPayload = Encoding.UTF8.GetString(rawBody);
            if (!TryReadEventMetadata(rawPayload, out var eventType, out var payloadEventId))
            {
                return new RazorpayWebhookReceiveResult(
                    false, false, ApiErrorCodes.ValidationError, "Webhook payload is invalid.", null);
            }

            var eventId = !string.IsNullOrWhiteSpace(eventIdHeader)
                ? eventIdHeader.Trim()
                : payloadEventId;

            if (string.IsNullOrWhiteSpace(eventId))
            {
                // Stable fallback so retries of the same body still dedupe when the header is absent.
                eventId = Convert.ToHexString(global::System.Security.Cryptography.SHA256.HashData(rawBody))[..32].ToLowerInvariant();
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var (stored, created) = await _inbox.TryEnqueueAsync(
                eventId, eventType, rawPayload, now, cancellationToken);

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.WebhookReceived,
                    PaymentLogSources.Webhook,
                    created ? PaymentLogStatus.Information : PaymentLogStatus.Warning,
                    created ? "Webhook event enqueued." : "Duplicate webhook event ignored.",
                    CorrelationId: correlationId,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["eventType"] = eventType,
                        ["razorpayEventId"] = eventId,
                        ["duplicate"] = (!created).ToString()
                    }),
                cancellationToken);

            if (created)
            {
                _signal.Notify();
            }

            _logger.LogInformation(
                "Razorpay webhook accepted. EventType {EventType}, Duplicate {Duplicate}.",
                eventType,
                !created);

            return new RazorpayWebhookReceiveResult(true, !created, null, null, stored.Id);
        }

        private static bool TryReadEventMetadata(string rawPayload, out string eventType, out string? eventId)
        {
            eventType = string.Empty;
            eventId = null;
            try
            {
                using var document = JsonDocument.Parse(rawPayload);
                if (!document.RootElement.TryGetProperty("event", out var eventNode))
                {
                    return false;
                }

                eventType = eventNode.GetString()?.Trim() ?? string.Empty;
                if (eventType.Length == 0)
                {
                    return false;
                }

                if (document.RootElement.TryGetProperty("id", out var idNode))
                {
                    eventId = idNode.GetString();
                }

                return true;
            }
            catch (JsonException)
            {
                return false;
            }
        }
    }

    /// <summary>Wakes the webhook worker when a new inbox row is stored.</summary>
    public sealed class RazorpayWebhookSignal
    {
        private readonly global::System.Threading.Channels.Channel<bool> _channel =
            global::System.Threading.Channels.Channel.CreateUnbounded<bool>(
                new global::System.Threading.Channels.UnboundedChannelOptions
                {
                    SingleReader = true,
                    SingleWriter = false
                });

        public void Notify()
        {
            _channel.Writer.TryWrite(true);
        }

        public global::System.Threading.Channels.ChannelReader<bool> Reader => _channel.Reader;
    }
}
