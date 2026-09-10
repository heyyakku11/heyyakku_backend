using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.Devices.Mapper;
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
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<RegisterDeviceRequest> _registerValidator;
        private readonly IValidator<UpdateDeviceRequest> _updateValidator;

        public DeviceService(
            IDeviceRepository devices,
            ISystemLogWriter systemLogWriter,
            IValidator<RegisterDeviceRequest> registerValidator,
            IValidator<UpdateDeviceRequest> updateValidator)
        {
            _devices = devices;
            _systemLogWriter = systemLogWriter;
            _registerValidator = registerValidator;
            _updateValidator = updateValidator;
        }

        public async Task<RegisterDeviceResult> RegisterAsync(
            Guid userId,
            RegisterDeviceRequest request,
            CancellationToken cancellationToken = default)
        {
            await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);

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

        public async Task<DeviceResponse> UpdateAsync(
            Guid userId,
            string installationId,
            UpdateDeviceRequest request,
            CancellationToken cancellationToken = default)
        {
            EnsureInstallationId(installationId);
            await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

            var device = await GetOwnedDeviceAsync(userId, installationId.Trim(), cancellationToken);
            DevicePlatform? platform = null;
            if (!string.IsNullOrWhiteSpace(request.Platform))
            {
                DeviceFieldParser.TryParsePlatform(request.Platform, out var parsedPlatform);
                platform = parsedPlatform;
            }

            var permission = ParseOptionalPermission(request.NotificationPermission);

            device.Update(
                platform,
                request.PushToken,
                updatePushToken: request.PushToken is not null,
                request.DeviceModel,
                request.OsVersion,
                request.AppVersion,
                request.AppBuild,
                request.Locale,
                request.Timezone,
                permission,
                request.IsActive);

            await _devices.SaveChangesAsync(cancellationToken);
            await LogAsync(
                SystemLogEventTypes.DeviceUpdated,
                "Device updated.",
                userId,
                device,
                cancellationToken);

            return device.ToResponse();
        }

        public async Task UnregisterAsync(
            Guid userId,
            string installationId,
            CancellationToken cancellationToken = default)
        {
            EnsureInstallationId(installationId);

            var device = await GetOwnedDeviceAsync(userId, installationId.Trim(), cancellationToken);
            if (device.IsActive || device.PushToken is not null)
            {
                device.Deactivate();
                await _devices.SaveChangesAsync(cancellationToken);
            }

            await LogAsync(
                SystemLogEventTypes.DeviceUnregistered,
                "Device unregistered.",
                userId,
                device,
                cancellationToken);
        }

        private async Task<Device> GetOwnedDeviceAsync(
            Guid userId,
            string installationId,
            CancellationToken cancellationToken)
        {
            var device = await _devices.GetByInstallationIdAndUserIdAsync(
                installationId,
                userId,
                cancellationToken);

            if (device is null)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "Device not found");
            }

            return device;
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

        private static void EnsureInstallationId(string installationId)
        {
            if (string.IsNullOrWhiteSpace(installationId))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Installation id is required.",
                    "installationId");
            }

            if (installationId.Length > 100)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Installation id must be at most 100 characters.",
                    "installationId");
            }
        }
    }
}
