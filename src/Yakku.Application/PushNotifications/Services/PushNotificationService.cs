using FluentValidation;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Application.PushNotifications.Services
{
    public class PushNotificationService : IPushNotificationService
    {
        private readonly IFirebaseNotificationProvider _firebase;
        private readonly IDeviceRepository _devices;
        private readonly IUserRepository _users;
        private readonly IValidator<NotificationPayload> _payloadValidator;
        private readonly IValidator<TestDeviceNotificationRequest> _deviceTestValidator;
        private readonly IValidator<TestUserNotificationRequest> _userTestValidator;

        public PushNotificationService(
            IFirebaseNotificationProvider firebase,
            IDeviceRepository devices,
            IUserRepository users,
            IValidator<NotificationPayload> payloadValidator,
            IValidator<TestDeviceNotificationRequest> deviceTestValidator,
            IValidator<TestUserNotificationRequest> userTestValidator)
        {
            _firebase = firebase;
            _devices = devices;
            _users = users;
            _payloadValidator = payloadValidator;
            _deviceTestValidator = deviceTestValidator;
            _userTestValidator = userTestValidator;
        }

        public async Task<PushDeviceNotificationResult> SendTestToDeviceAsync(
            TestDeviceNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            await _deviceTestValidator.ValidateAndThrowAsync(request, cancellationToken);
            return await SendToDeviceAsync(
                request.InstallationId,
                request.Platform,
                request.Notification,
                cancellationToken);
        }

        public async Task<PushUserNotificationResult> SendTestToUserAsync(
            TestUserNotificationRequest request,
            CancellationToken cancellationToken = default)
        {
            await _userTestValidator.ValidateAndThrowAsync(request, cancellationToken);
            return await SendToUserAsync(request.UserId, request.Notification, cancellationToken);
        }

        public async Task<PushDeviceNotificationResult> SendToDeviceAsync(
            string installationId,
            string platform,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            await _payloadValidator.ValidateAndThrowAsync(payload, cancellationToken);

            if (!DeviceFieldParser.TryParsePlatform(platform, out var parsedPlatform))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Platform must be android or ios.",
                    "platform");
            }

            var device = await _devices.GetByInstallationIdAndPlatformAsync(
                installationId.Trim(),
                parsedPlatform,
                cancellationToken);

            if (device is null)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "Device not found.",
                    "installationId");
            }

            return await SendToDeviceAsync(device, payload, cancellationToken);
        }

        public async Task<PushDeviceNotificationResult> SendToDeviceAsync(
            Device device,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            await _payloadValidator.ValidateAndThrowAsync(payload, cancellationToken);
            EnsureEligible(device);

            var sendResult = await _firebase.SendAsync(device.PushToken!, payload, cancellationToken);
            var deactivated = false;

            if (sendResult.IsInvalidToken)
            {
                device.Deactivate();
                await _devices.SaveChangesAsync(cancellationToken);
                deactivated = true;
            }

            if (!sendResult.Success && !sendResult.IsInvalidToken)
            {
                throw new AppException(
                    502,
                    ApiErrorCodes.InternalServerError,
                    "Failed to send push notification.");
            }

            if (!sendResult.Success)
            {
                throw new AppException(
                    502,
                    ApiErrorCodes.InternalServerError,
                    "Push token is invalid or unregistered.");
            }

            return new PushDeviceNotificationResult
            {
                DeviceId = device.Id,
                InstallationId = device.InstallationId,
                Platform = device.Platform.ToString().ToLowerInvariant(),
                Success = true,
                FirebaseMessageId = sendResult.MessageId,
                InvalidTokenDeactivated = deactivated
            };
        }

        public async Task<PushUserNotificationResult> SendToDevicesAsync(
            IReadOnlyList<Device> devices,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            await _payloadValidator.ValidateAndThrowAsync(payload, cancellationToken);

            var eligible = devices
                .Where(IsEligible)
                .ToList();

            if (eligible.Count == 0)
            {
                return new PushUserNotificationResult
                {
                    UserId = devices.FirstOrDefault()?.UserId ?? Guid.Empty,
                    TotalDevices = devices.Count,
                    EligibleDevices = 0,
                    Successful = 0,
                    Failed = 0,
                    InvalidTokensDeactivated = 0
                };
            }

            var tokens = eligible.Select(device => device.PushToken!).ToList();
            var sendResults = await _firebase.SendToManyAsync(tokens, payload, cancellationToken);
            var successful = 0;
            var failed = 0;
            var deactivated = 0;

            for (var index = 0; index < eligible.Count; index++)
            {
                var device = eligible[index];
                var result = sendResults[index];

                if (result.Success)
                {
                    successful++;
                    continue;
                }

                failed++;

                if (!result.IsInvalidToken)
                {
                    continue;
                }

                device.Deactivate();
                deactivated++;
            }

            if (deactivated > 0)
            {
                await _devices.SaveChangesAsync(cancellationToken);
            }

            return new PushUserNotificationResult
            {
                UserId = eligible[0].UserId ?? Guid.Empty,
                TotalDevices = devices.Count,
                EligibleDevices = eligible.Count,
                Successful = successful,
                Failed = failed,
                InvalidTokensDeactivated = deactivated
            };
        }

        public async Task<PushUserNotificationResult> SendToUserAsync(
            Guid userId,
            NotificationPayload payload,
            CancellationToken cancellationToken = default)
        {
            await _payloadValidator.ValidateAndThrowAsync(payload, cancellationToken);

            var user = await _users.GetByIdAsync(userId, cancellationToken);
            if (user is null)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "User not found.",
                    "userId");
            }

            var devices = await _devices.GetByUserIdAsync(userId, cancellationToken);
            if (devices.Count == 0)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "No registered devices found for this user.",
                    "userId");
            }

            var eligibleCount = devices.Count(IsEligible);
            if (eligibleCount == 0)
            {
                var hasActive = devices.Any(device => device.IsActive);
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    hasActive
                        ? "No devices with a push token are available for this user."
                        : "No active devices found for this user.",
                    "userId");
            }

            var result = await SendToDevicesAsync(devices, payload, cancellationToken);
            return new PushUserNotificationResult
            {
                UserId = userId,
                TotalDevices = result.TotalDevices,
                EligibleDevices = result.EligibleDevices,
                Successful = result.Successful,
                Failed = result.Failed,
                InvalidTokensDeactivated = result.InvalidTokensDeactivated
            };
        }

        private static void EnsureEligible(Device device)
        {
            if (!device.IsActive)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Device is inactive.",
                    "installationId");
            }

            if (string.IsNullOrWhiteSpace(device.PushToken))
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Device does not have a push token.",
                    "installationId");
            }
        }

        private static bool IsEligible(Device device)
        {
            return device.IsActive && !string.IsNullOrWhiteSpace(device.PushToken);
        }
    }
}
