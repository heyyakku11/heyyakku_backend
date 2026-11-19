using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.Payments.Interfaces;
using Yakku.Application.Payments.Services;

namespace Yakku.Infrastructure.Payments
{
    /// <summary>
    /// Polls the Razorpay webhook inbox and processes events through the shared payment fulfillment path.
    /// </summary>
    public sealed class RazorpayWebhookWorker : BackgroundService
    {
        public const int MaxRetries = 8;
        public static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);
        public static readonly TimeSpan StaleProcessingAfter = TimeSpan.FromMinutes(5);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly RazorpayWebhookSignal _signal;
        private readonly ILogger<RazorpayWebhookWorker> _logger;

        public RazorpayWebhookWorker(
            IServiceScopeFactory scopeFactory,
            RazorpayWebhookSignal signal,
            ILogger<RazorpayWebhookWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _signal = signal;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    var handled = 0;
                    try
                    {
                        handled = await DrainAsync(stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Razorpay webhook worker run failed.");
                    }

                    if (handled > 0)
                    {
                        continue;
                    }

                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(PollInterval);
                        await _signal.Reader.WaitToReadAsync(timeout.Token);
                        while (_signal.Reader.TryRead(out _))
                        {
                        }
                    }
                    catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
                    {
                        // Poll interval elapsed.
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
            }
        }

        private async Task<int> DrainAsync(CancellationToken cancellationToken)
        {
            var handled = 0;
            await using var scope = _scopeFactory.CreateAsyncScope();
            var inbox = scope.ServiceProvider.GetRequiredService<IRazorpayWebhookInbox>();
            var processor = scope.ServiceProvider.GetRequiredService<IRazorpayWebhookProcessor>();
            var time = scope.ServiceProvider.GetRequiredService<TimeProvider>();

            await inbox.ReclaimStaleProcessingAsync(
                StaleProcessingAfter, time.GetUtcNow().UtcDateTime, cancellationToken);

            while (!cancellationToken.IsCancellationRequested)
            {
                var next = await inbox.ClaimNextAsync(MaxRetries, cancellationToken);
                if (next is null)
                {
                    break;
                }

                await processor.ProcessAsync(next.Id, cancellationToken);
                handled++;
            }

            return handled;
        }
    }
}
