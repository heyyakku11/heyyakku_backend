using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Payments.Interfaces;
using Yakku.Infrastructure.Jobs;

namespace Yakku.Infrastructure.Payments
{
    public sealed class PaymentReconciliationWorker : PeriodicJobWorker
    {
        public PaymentReconciliationWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<PaymentReconciliationWorker> logger)
            : base(
                scopeFactory,
                logger,
                jobName: "payment-reconciliation",
                initialDelay: TimeSpan.FromMinutes(2),
                interval: TimeSpan.FromMinutes(5))
        {
        }

        protected override Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            return services.GetRequiredService<IPaymentReconciliationService>().RunAsync(cancellationToken);
        }
    }
}
