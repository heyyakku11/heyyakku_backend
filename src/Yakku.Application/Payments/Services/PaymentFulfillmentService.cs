using Yakku.Application.Payments.Interfaces;

namespace Yakku.Application.Payments.Services
{
    /// <summary>Thin application wrapper over the transactional fulfillment repository.</summary>
    public sealed class PaymentFulfillmentService : IPaymentFulfillmentService
    {
        private readonly IPaymentFulfillmentRepository _repository;
        private readonly IPaymentLogWriter _paymentLogs;

        public PaymentFulfillmentService(
            IPaymentFulfillmentRepository repository,
            IPaymentLogWriter paymentLogs)
        {
            _repository = repository;
            _paymentLogs = paymentLogs;
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
                        Domain.Enums.PaymentLogStatus.Success,
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

                if (result.PurchaseType == Domain.Enums.PaymentPurchaseType.UserPlan)
                {
                    await _paymentLogs.WriteAsync(
                        new PaymentLogEntry(
                            PaymentLogEventTypes.EntitlementCreated,
                            PaymentLogSources.System,
                            Domain.Enums.PaymentLogStatus.Success,
                            "User plan entitlement activated.",
                            PaymentOrderId: result.PaymentOrderId,
                            CorrelationId: command.CorrelationId,
                            Metadata: new Dictionary<string, string?>
                            {
                                ["planCode"] = result.PlanCode,
                                ["expiresAt"] = result.PlanExpiresAt?.ToString("O")
                            }),
                        cancellationToken);
                }
                else
                {
                    await _paymentLogs.WriteAsync(
                        new PaymentLogEntry(
                            PaymentLogEventTypes.CardUnlockCreated,
                            PaymentLogSources.System,
                            Domain.Enums.PaymentLogStatus.Success,
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
            }

            return result;
        }
    }
}
