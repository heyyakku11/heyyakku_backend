using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Infrastructure.System
{
    public class SystemLogWriter : ISystemLogWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<SystemLogWriter> _logger;

        public SystemLogWriter(IServiceScopeFactory scopeFactory, ILogger<SystemLogWriter> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task WriteAsync(
            SystemLogWriteRequest request,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<YakkuDbContext>();
                context.SystemLogs.Add(new SystemLog(
                    request.Level,
                    Truncate(request.EventType, 64),
                    Truncate(request.Message, 500),
                    SerializeDetails(request.Details),
                    request.UserId,
                    request.GuestId,
                    TruncateNullable(request.Path, 256)));
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to persist system log {EventType}",
                    request.EventType);
            }
        }

        private static string? SerializeDetails(object? details)
        {
            if (details is null)
            {
                return null;
            }

            var json = details is string text
                ? text
                : JsonSerializer.Serialize(details, JsonOptions);

            return TruncateNullable(json, 4000);
        }

        private static string Truncate(string? value, int maxLength)
        {
            return TruncateNullable(value, maxLength) ?? string.Empty;
        }

        private static string? TruncateNullable(string? value, int maxLength)
        {
            if (string.IsNullOrEmpty(value))
            {
                return value;
            }

            return value.Length <= maxLength ? value : value[..maxLength];
        }
    }
}
