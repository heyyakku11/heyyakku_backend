namespace Yakku.Application.Payments.Interfaces
{
    public interface IPaymentReconciliationService
    {
        Task<PaymentReconciliationRunResult> RunAsync(CancellationToken cancellationToken = default);
    }

    public sealed record PaymentReconciliationRunResult(
        int ExpiredOrders,
        int CapturedFulfillments,
        int MismatchesLogged,
        int ExhaustedWebhooksLogged);
}
