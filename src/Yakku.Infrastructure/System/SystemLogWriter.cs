using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
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
                    MapSeverity(request.Level),
                    MapEventType(request.EventType),
                    request.Message,
                    SerializeDetails(request.Details),
                    request.UserId,
                    request.GuestId));
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

        private static LogSeverity MapSeverity(SystemLogLevel level)
        {
            return level switch
            {
                SystemLogLevel.Information => LogSeverity.Info,
                SystemLogLevel.Warning => LogSeverity.Warning,
                SystemLogLevel.Error => LogSeverity.Error,
                _ => LogSeverity.Info
            };
        }

        private static SystemEventType MapEventType(string eventType)
        {
            return eventType switch
            {
                SystemLogEventTypes.OtpRequested => SystemEventType.Authentication,
                SystemLogEventTypes.OtpInvalid => SystemEventType.Authentication,
                SystemLogEventTypes.UserRegistered => SystemEventType.Authentication,
                SystemLogEventTypes.UserLoggedIn => SystemEventType.Authentication,
                SystemLogEventTypes.SessionRefreshed => SystemEventType.Authentication,
                SystemLogEventTypes.SessionRevoked => SystemEventType.Authentication,
                SystemLogEventTypes.PollCreated => SystemEventType.Poll,
                SystemLogEventTypes.PollClosed => SystemEventType.Poll,
                SystemLogEventTypes.PollDeleted => SystemEventType.Poll,
                SystemLogEventTypes.VoteCast => SystemEventType.Vote,
                SystemLogEventTypes.VoteRejectedAlreadyVoted => SystemEventType.Vote,
                SystemLogEventTypes.DeviceRegistered => SystemEventType.Device,
                SystemLogEventTypes.DeviceUpdated => SystemEventType.Device,
                SystemLogEventTypes.DeviceUnregistered => SystemEventType.Device,
                SystemLogEventTypes.UnhandledException => SystemEventType.System,
                _ => SystemEventType.System
            };
        }

        private static string? SerializeDetails(object? details)
        {
            if (details is null)
            {
                return null;
            }

            return details is string text
                ? text
                : JsonSerializer.Serialize(details, JsonOptions);
        }
    }
}
