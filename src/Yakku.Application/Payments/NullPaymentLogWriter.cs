namespace Yakku.Application.Payments
{
    /// <summary>No-op writer used until Infrastructure replaces it, and in unit tests that do not care about audit.</summary>
    public sealed class NullPaymentLogWriter : IPaymentLogWriter
    {
        public Task WriteAsync(PaymentLogEntry entry, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
