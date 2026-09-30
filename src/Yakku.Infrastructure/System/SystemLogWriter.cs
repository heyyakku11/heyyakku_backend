using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
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
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<SystemLogWriter> _logger;

        public SystemLogWriter(
            IServiceScopeFactory scopeFactory,
            IHttpContextAccessor httpContextAccessor,
            ILogger<SystemLogWriter> logger)
        {
            _scopeFactory = scopeFactory;
            _httpContextAccessor = httpContextAccessor;
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
                    request.GuestId,
                    ResolveIpAddress(request),
                    ResolveUserAgent(request)));
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

        private IPAddress? ResolveIpAddress(SystemLogWriteRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.IpAddress)
                && IPAddress.TryParse(request.IpAddress.Trim(), out var parsed))
            {
                return parsed;
            }

            return _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress;
        }

        private string? ResolveUserAgent(SystemLogWriteRequest request)
        {
            if (!string.IsNullOrWhiteSpace(request.UserAgent))
            {
                return request.UserAgent.Trim();
            }

            var header = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
            return string.IsNullOrWhiteSpace(header) ? null : header;
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
                SystemLogEventTypes.OtpEmailPermanentlyFailed => SystemEventType.Authentication,
                SystemLogEventTypes.OtpEmailWorkerFault => SystemEventType.System,
                SystemLogEventTypes.UserRegistered => SystemEventType.Authentication,
                SystemLogEventTypes.UserLoggedIn => SystemEventType.Authentication,
                SystemLogEventTypes.SessionRefreshed => SystemEventType.Authentication,
                SystemLogEventTypes.AdminRegistered => SystemEventType.Authentication,
                SystemLogEventTypes.AdminSessionRefreshed => SystemEventType.Authentication,
                SystemLogEventTypes.AdminSessionRevoked => SystemEventType.Authentication,
                SystemLogEventTypes.SessionRevoked => SystemEventType.Authentication,
                SystemLogEventTypes.PollCreated => SystemEventType.Poll,
                SystemLogEventTypes.PollClosed => SystemEventType.Poll,
                SystemLogEventTypes.PollDeleted => SystemEventType.Poll,
                SystemLogEventTypes.VoteCast => SystemEventType.Vote,
                SystemLogEventTypes.VoteRejectedAlreadyVoted => SystemEventType.Vote,
                SystemLogEventTypes.DeviceRegistered => SystemEventType.Device,
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
