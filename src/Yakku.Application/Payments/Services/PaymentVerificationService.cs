using FluentValidation;
using Microsoft.Extensions.Logging;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Configuration;
using Yakku.Application.Payments.DTOs;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Services
{
    public sealed class PaymentVerificationService : IPaymentVerificationService
    {
        private readonly IPaymentOrderRepository _orders;
        private readonly IPaymentFulfillmentRepository _fulfillmentRepository;
        private readonly IPaymentFulfillmentService _fulfillment;
        private readonly IRazorpayClient _razorpay;
        private readonly IPaymentLogWriter _paymentLogs;
        private readonly RazorpayOptions _razorpayOptions;
        private readonly IValidator<VerifyPaymentRequest> _validator;
        private readonly TimeProvider _time;
        private readonly ILogger<PaymentVerificationService> _logger;

        public PaymentVerificationService(
            IPaymentOrderRepository orders,
            IPaymentFulfillmentRepository fulfillmentRepository,
            IPaymentFulfillmentService fulfillment,
            IRazorpayClient razorpay,
            IPaymentLogWriter paymentLogs,
            RazorpayOptions razorpayOptions,
            IValidator<VerifyPaymentRequest> validator,
            TimeProvider time,
            ILogger<PaymentVerificationService> logger)
        {
            _orders = orders;
            _fulfillmentRepository = fulfillmentRepository;
            _fulfillment = fulfillment;
            _razorpay = razorpay;
            _paymentLogs = paymentLogs;
            _razorpayOptions = razorpayOptions;
            _validator = validator;
            _time = time;
            _logger = logger;
        }

        public async Task<VerifyPaymentResponse> VerifyAsync(
            Guid userId,
            VerifyPaymentRequest request,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            if (!_razorpay.IsEnabled || !_razorpayOptions.Enabled)
            {
                throw new AppException(
                    503,
                    ApiErrorCodes.PaymentProviderDisabled,
                    "Payments are temporarily unavailable.");
            }

            var razorpayOrderId = request.RazorpayOrderId!.Trim();
            var razorpayPaymentId = request.RazorpayPaymentId!.Trim();
            var signature = request.RazorpaySignature!.Trim();
            var now = _time.GetUtcNow().UtcDateTime;

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.PaymentVerificationRequested,
                    PaymentLogSources.Api,
                    PaymentLogStatus.Information,
                    "Payment verification requested.",
                    UserId: userId,
                    CorrelationId: correlationId,
                    RazorpayOrderId: razorpayOrderId,
                    RazorpayPaymentId: razorpayPaymentId),
                cancellationToken);

            var order = await _orders.FindByRazorpayOrderIdAsync(razorpayOrderId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Payment order not found.");

            if (order.UserId != userId)
            {
                throw new AppException(404, ApiErrorCodes.NotFound, "Payment order not found.");
            }

            if (!RazorpayPaymentSignature.IsValid(
                    razorpayOrderId, razorpayPaymentId, signature, _razorpayOptions.KeySecret))
            {
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.PaymentSignatureVerificationFailed,
                        PaymentLogSources.Api,
                        PaymentLogStatus.Failure,
                        "Payment signature verification failed.",
                        PaymentOrderId: order.Id,
                        UserId: userId,
                        CorrelationId: correlationId,
                        RazorpayOrderId: razorpayOrderId,
                        RazorpayPaymentId: razorpayPaymentId),
                    cancellationToken);

                throw new AppException(
                    400,
                    ApiErrorCodes.PaymentSignatureInvalid,
                    "Payment signature is invalid.");
            }

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.PaymentSignatureVerified,
                    PaymentLogSources.Api,
                    PaymentLogStatus.Success,
                    "Payment signature verified.",
                    PaymentOrderId: order.Id,
                    UserId: userId,
                    CorrelationId: correlationId,
                    RazorpayOrderId: razorpayOrderId,
                    RazorpayPaymentId: razorpayPaymentId),
                cancellationToken);

            // Already fulfilled for this payment (or order paid): return idempotently.
            var existingAttempt = await _fulfillmentRepository.FindAttemptByRazorpayPaymentIdAsync(
                razorpayPaymentId, cancellationToken);
            if (existingAttempt is { Status: PaymentAttemptStatus.Captured }
                || order.Status == PaymentOrderStatus.Paid)
            {
                var fulfilled = await _fulfillment.FulfillCapturedPaymentAsync(
                    new CapturedPaymentFulfillmentCommand(
                        order.Id,
                        razorpayOrderId,
                        razorpayPaymentId,
                        order.AmountInPaise,
                        order.Currency,
                        existingAttempt?.PaymentMethod,
                        existingAttempt?.RazorpayStatus ?? "captured",
                        existingAttempt?.CapturedAt ?? now,
                        correlationId),
                    cancellationToken);
                return ToResponse(fulfilled, razorpayPaymentId);
            }

            var payment = await _razorpay.GetPaymentAsync(razorpayPaymentId, cancellationToken);
            if (!payment.Succeeded || string.IsNullOrWhiteSpace(payment.RazorpayPaymentId))
            {
                _logger.LogWarning(
                    "Razorpay payment fetch failed for {PaymentId}. ErrorCode {ErrorCode}.",
                    razorpayPaymentId,
                    payment.ErrorCode);
                throw new AppException(
                    502,
                    ApiErrorCodes.PaymentGatewayError,
                    "Unable to verify payment with the payment provider. Please try again.");
            }

            if (!string.Equals(payment.RazorpayOrderId, razorpayOrderId, StringComparison.Ordinal))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.PaymentOrderMismatch,
                    "Payment does not belong to this order.");
            }

            if (payment.AmountInPaise is not int amount || amount != order.AmountInPaise)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.PaymentAmountMismatch,
                    "Payment amount does not match the order.");
            }

            if (!string.Equals(payment.Currency, order.Currency, StringComparison.OrdinalIgnoreCase))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.PaymentCurrencyMismatch,
                    "Payment currency does not match the order.");
            }

            var status = payment.Status?.Trim().ToLowerInvariant() ?? string.Empty;
            if (status is "failed" or "cancelled")
            {
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.PaymentFailed,
                        PaymentLogSources.Api,
                        PaymentLogStatus.Failure,
                        "Payment failed at the gateway.",
                        PaymentOrderId: order.Id,
                        UserId: userId,
                        CorrelationId: correlationId,
                        RazorpayOrderId: razorpayOrderId,
                        RazorpayPaymentId: razorpayPaymentId,
                        Metadata: new Dictionary<string, string?>
                        {
                            ["razorpayStatus"] = status,
                            ["errorCode"] = payment.ErrorCode
                        }),
                    cancellationToken);

                throw new AppException(
                    402,
                    ApiErrorCodes.PaymentNotCaptured,
                    "Payment was not successful.");
            }

            if (!payment.Captured && status != "captured")
            {
                await _paymentLogs.WriteAsync(
                    new PaymentLogEntry(
                        PaymentLogEventTypes.PaymentAuthorized,
                        PaymentLogSources.Api,
                        PaymentLogStatus.Warning,
                        "Payment is authorized but not captured; access was not granted.",
                        PaymentOrderId: order.Id,
                        UserId: userId,
                        CorrelationId: correlationId,
                        RazorpayOrderId: razorpayOrderId,
                        RazorpayPaymentId: razorpayPaymentId,
                        Metadata: new Dictionary<string, string?> { ["razorpayStatus"] = status }),
                    cancellationToken);

                throw new AppException(
                    402,
                    ApiErrorCodes.PaymentNotCaptured,
                    "Payment has not been captured yet.");
            }

            var result = await _fulfillment.FulfillCapturedPaymentAsync(
                new CapturedPaymentFulfillmentCommand(
                    order.Id,
                    razorpayOrderId,
                    razorpayPaymentId,
                    order.AmountInPaise,
                    order.Currency,
                    payment.Method,
                    status,
                    now,
                    correlationId),
                cancellationToken);

            return ToResponse(result, razorpayPaymentId);
        }

        private static VerifyPaymentResponse ToResponse(PaymentFulfillmentResult result, string razorpayPaymentId)
        {
            return new VerifyPaymentResponse
            {
                OrderId = result.PaymentOrderId,
                Status = PaymentOrderStatus.Paid.ToString(),
                AlreadyFulfilled = result.AlreadyFulfilled,
                PurchaseType = result.PurchaseType.ToString(),
                PlanCode = result.PlanCode,
                PlanExpiresAt = result.PlanExpiresAt,
                ContentType = result.ContentType switch
                {
                    ShareContentType.Poll => "poll",
                    ShareContentType.Check => "check",
                    _ => null
                },
                ContentId = result.ContentId,
                ConversationId = result.ConversationId,
                TopUpCredits = result.TopUpCredits,
                RazorpayPaymentId = razorpayPaymentId
            };
        }
    }
}
