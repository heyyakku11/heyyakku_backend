using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Yakku.Application.Polls.Interfaces;

namespace Yakku.Infrastructure.Storage
{
    public sealed class MediaUploadCleanupWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<MediaUploadCleanupWorker> _logger;
        private readonly TimeSpan _interval;

        public MediaUploadCleanupWorker(
            IServiceScopeFactory scopeFactory,
            ILogger<MediaUploadCleanupWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = ReadInterval();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    var cleanup = scope.ServiceProvider.GetRequiredService<IMediaUploadCleanupService>();
                    await cleanup.CleanupAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    _logger.LogWarning(
                        "Media upload cleanup did not finish ({ExceptionType}).",
                        exception.GetType().Name);
                }
            }
        }

        private static TimeSpan ReadInterval()
        {
            var raw = Environment.GetEnvironmentVariable("POLL_MEDIA_CLEANUP_INTERVAL_MINUTES");
            return int.TryParse(raw, out var minutes) && minutes > 0
                ? TimeSpan.FromMinutes(minutes)
                : TimeSpan.FromMinutes(15);
        }
    }
}
