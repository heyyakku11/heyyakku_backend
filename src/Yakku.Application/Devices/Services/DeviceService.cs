using FluentValidation;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.Devices.Mapper;
using Yakku.Application.NotificationPreferences.Interfaces;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Devices.Services
{
    public class DeviceService : IDeviceService
    {
        private readonly IDeviceRepository _devices;
        private readonly IUserSessionRepository _sessions;
        private readonly INotificationPreferenceRepository _preferences;
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<RegisterDeviceRequest> _registerValidator;

        public DeviceService(
            IDeviceRepository devices,
            IUserSessionRepository sessions,
            INotificationPreferenceRepository preferences,
            ISystemLogWriter systemLogWriter,
            IValidator<RegisterDeviceRequest> registerValidator)
        {
            _devices = devices;
            _sessions = sessions;
            _preferences = preferences;
            _systemLogWriter = systemLogWriter;
            _registerValidator = registerValidator;
        }

        public async Task<RegisterDeviceResult> RegisterAsync(
            Guid userId,
            RegisterDeviceRequest request,
            Guid sessionId,
            CancellationToken cancellationToken = default)
        {
            await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);
            EnsureSessionId(sessionId);

            var installationId = request.InstallationId.Trim();
            DeviceFieldParser.TryParsePlatform(request.Platform, out var platform);
            var permission = ParseOptionalPermission(request.NotificationPermission);

            var existing = await _devices.GetByInstallationIdAsync(installationId, cancellationToken);
            if (existing is not null)
            {
                existing.Register(
                    userId,
                    platform,
                    request.PushToken,
                    request.DeviceModel,
                    request.OsVersion,
                    request.AppVersion,
                    request.AppBuild,
                    request.Locale,
                    request.Timezone,
                    permission);
                await LinkSessionAsync(userId, sessionId, existing.Id, cancellationToken);
                await EnsureNotificationPreferencesAsync(userId, existing, cancellationToken);
                await _devices.SaveChangesAsync(cancellationToken);
                await LogAsync(
                    SystemLogEventTypes.DeviceRegistered,
                    "Device registration updated.",
                    userId,
                    existing,
                    cancellationToken);

                return new RegisterDeviceResult
                {
                    Device = existing.ToResponse(),
                    Created = false
                };
            }

            var device = new Device(installationId, platform, userId);
            device.Register(
                userId,
                platform,
                request.PushToken,
                request.DeviceModel,
                request.OsVersion,
                request.AppVersion,
                request.AppBuild,
                request.Locale,
                request.Timezone,
                permission);

            await LinkSessionAsync(userId, sessionId, device.Id, cancellationToken);
            await EnsureNotificationPreferencesAsync(userId, device, cancellationToken);
            await _devices.AddAsync(device, cancellationToken);
            try
            {
                await _devices.SaveChangesAsync(cancellationToken);
            }
            catch (AppException exception) when (exception.ErrorCode == ApiErrorCodes.Conflict)
            {
                var raced = await _devices.GetByInstallationIdAsync(installationId, cancellationToken);
                if (raced is null)
                {
                    throw;
                }

                raced.Register(
                    userId,
                    platform,
                    request.PushToken,
                    request.DeviceModel,
                    request.OsVersion,
                    request.AppVersion,
                    request.AppBuild,
                    request.Locale,
                    request.Timezone,
                    permission);
                await LinkSessionAsync(userId, sessionId, raced.Id, cancellationToken);
                await EnsureNotificationPreferencesAsync(userId, raced, cancellationToken);
                await _devices.SaveChangesAsync(cancellationToken);
                await LogAsync(
                    SystemLogEventTypes.DeviceRegistered,
                    "Device registration updated.",
                    userId,
                    raced,
                    cancellationToken);

                return new RegisterDeviceResult
                {
                    Device = raced.ToResponse(),
                    Created = false
                };
            }

            await LogAsync(
                SystemLogEventTypes.DeviceRegistered,
                "Device registered.",
                userId,
                device,
                cancellationToken);

            return new RegisterDeviceResult
            {
                Device = device.ToResponse(),
                Created = true
            };
        }

        private async Task EnsureNotificationPreferencesAsync(
            Guid userId,
            Device device,
            CancellationToken cancellationToken)
        {
            if (device.NotificationPermission != NotificationPermissionStatus.Granted
                || string.IsNullOrWhiteSpace(device.PushToken))
            {
                return;
            }

            var existing = await _preferences.GetByUserIdAsync(userId, cancellationToken);
            if (existing is not null)
            {
                return;
            }

            await _preferences.AddAsync(new NotificationPreference(userId), cancellationToken);
        }

        private async Task LinkSessionAsync(
            Guid userId,
            Guid sessionId,
            Guid deviceId,
            CancellationToken cancellationToken)
        {
            var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
            if (session is null || session.UserId != userId)
            {
                throw new AppException(
                    401,
                    ApiErrorCodes.Unauthorized,
                    "Invalid or expired session.");
            }

            if (session.DeviceId == deviceId)
            {
                return;
            }

            session.AssignDevice(deviceId);
        }

        private static void EnsureSessionId(Guid sessionId)
        {
            if (sessionId == Guid.Empty)
            {
                throw new AppException(
                    401,
                    ApiErrorCodes.Unauthorized,
                    "Unauthorized.");
            }
        }

        private Task LogAsync(
            string eventType,
            string message,
            Guid userId,
            Device device,
            CancellationToken cancellationToken)
        {
            return _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = eventType,
                    Message = message,
                    UserId = userId,
                    Details = new
                    {
                        deviceId = device.Id,
                        installationId = device.InstallationId,
                        isActive = device.IsActive
                    }
                },
                cancellationToken);
        }

        private static NotificationPermissionStatus? ParseOptionalPermission(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            DeviceFieldParser.TryParseNotificationPermission(value, out var permission);
            return permission;
        }
    }
}
