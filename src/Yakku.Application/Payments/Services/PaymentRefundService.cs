using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Payments.DTOs;
using Yakku.Application.Payments.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Payments.Services
{
    /// <summary>
    /// Admin refunds via Razorpay. Does not revoke entitlements or permanent unlocks — document that as a
    /// separate explicit admin action when product requires it.
    /// </summary>
    public sealed class PaymentRefundService : IPaymentRefundService
    {
        private readonly IPaymentRefundRepository _repository;
        private readonly IRazorpayClient _razorpay;
        private readonly IPaymentLogWriter _paymentLogs;
        private readonly TimeProvider _time;

        public PaymentRefundService(
            IPaymentRefundRepository repository,
            IRazorpayClient razorpay,
            IPaymentLogWriter paymentLogs,
            TimeProvider time)
        {
            _repository = repository;
            _razorpay = razorpay;
            _paymentLogs = paymentLogs;
            _time = time;
        }

        public async Task<PaymentRefundResponse> CreateRefundAsync(
            Guid paymentAttemptId,
            CreatePaymentRefundRequest request,
            string? correlationId,
            CancellationToken cancellationToken = default)
        {
            if (!_razorpay.IsEnabled)
            {
                throw new AppException(
                    503,
                    ApiErrorCodes.PaymentProviderDisabled,
                    "Payments are temporarily unavailable.");
            }

            var attempt = await _repository.FindAttemptAsync(paymentAttemptId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Payment attempt not found.");

            if (attempt.Status != PaymentAttemptStatus.Captured
                || string.IsNullOrWhiteSpace(attempt.RazorpayPaymentId))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Only captured payments can be refunded.");
            }

            var existing = await _repository.FindProcessedByAttemptAsync(attempt.Id, cancellationToken);
            if (existing is not null)
            {
                return Map(existing);
            }

            var order = await _repository.FindOrderAsync(attempt.PaymentOrderId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Payment order not found.");

            var amount = request.AmountInPaise ?? attempt.AmountInPaise;
            if (amount <= 0 || amount > attempt.AmountInPaise)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Refund amount must be between 1 and the captured amount.",
                    "amountInPaise");
            }

            var now = _time.GetUtcNow().UtcDateTime;
            var refund = PaymentRefund.Request(attempt.Id, order.Id, amount, request.Reason, now);
            await _repository.AddAsync(refund, cancellationToken);

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.RefundRequested,
                    PaymentLogSources.Admin,
                    PaymentLogStatus.Information,
                    "Admin refund requested.",
                    PaymentOrderId: order.Id,
                    PaymentAttemptId: attempt.Id,
                    CorrelationId: correlationId,
                    RazorpayPaymentId: attempt.RazorpayPaymentId,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["amountInPaise"] = amount.ToString(),
                        ["reason"] = request.Reason
                    }),
                cancellationToken);

            var gateway = await _razorpay.CreateRefundAsync(
                new RazorpayCreateRefundRequest(attempt.RazorpayPaymentId, amount, request.Reason),
                cancellationToken);

            if (!gateway.Succeeded || string.IsNullOrWhiteSpace(gateway.RazorpayRefundId))
            {
                refund.MarkFailed(gateway.ErrorCode, gateway.ErrorMessage, now);
                await _repository.SaveAsync(cancellationToken);
                throw new AppException(
                    502,
                    ApiErrorCodes.PaymentProviderError,
                    gateway.ErrorMessage ?? "Razorpay refund failed.");
            }

            refund.MarkProcessed(gateway.RazorpayRefundId, now);
            await _repository.SaveAsync(cancellationToken);

            await _paymentLogs.WriteAsync(
                new PaymentLogEntry(
                    PaymentLogEventTypes.RefundProcessed,
                    PaymentLogSources.Admin,
                    PaymentLogStatus.Success,
                    "Admin refund processed at Razorpay. Access was not revoked automatically.",
                    PaymentOrderId: order.Id,
                    PaymentAttemptId: attempt.Id,
                    CorrelationId: correlationId,
                    RazorpayPaymentId: attempt.RazorpayPaymentId,
                    Metadata: new Dictionary<string, string?>
                    {
                        ["razorpayRefundId"] = gateway.RazorpayRefundId,
                        ["amountInPaise"] = amount.ToString()
                    }),
                cancellationToken);

            return Map(refund);
        }

        private static PaymentRefundResponse Map(PaymentRefund refund)
        {
            return new PaymentRefundResponse
            {
                RefundId = refund.Id,
                PaymentAttemptId = refund.PaymentAttemptId,
                PaymentOrderId = refund.PaymentOrderId,
                RazorpayRefundId = refund.RazorpayRefundId,
                AmountInPaise = refund.AmountInPaise,
                Status = refund.Status.ToString(),
                Reason = refund.Reason,
                CreatedAt = refund.CreatedAt,
                ProcessedAt = refund.ProcessedAt
            };
        }
    }
}
