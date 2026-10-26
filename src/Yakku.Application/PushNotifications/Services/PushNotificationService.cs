using System.Diagnostics;
using FluentValidation;
using Yakku.Application.Audit;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

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
        private readonly IAuditLogWriter _audit;

        public PushNotificationService(
            IFirebaseNotificationProvider firebase,
            IDeviceRepository devices,
            IUserRepository users,
            IValidator<NotificationPayload> payloadValidator,
            IValidator<TestDeviceNotificationRequest> deviceTestValidator,
            IValidator<TestUserNotificationRequest> userTestValidator,
            IAuditLogWriter audit)
        {
            _audit = audit;
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

            // The caller's copy may be stale (queued work, a cached list). Decide from the database, at send time,
            // whether this installation still belongs to a signed-in account that wants push.
            // The payload was meant for the account the caller saw on the device; if the installation now belongs
            // to anyone else, nothing is sent.
            var current = (await _devices.GetPushEligibleByIdsAsync([device.Id], DateTime.UtcNow, cancellationToken))
                .FirstOrDefault(fresh => fresh.UserId == device.UserId);
            if (current is null)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Device is not eligible for push: it is inactive, has no push token, has no live signed-in " +
                    "account, or the account has push disabled.",
                    "installationId");
            }

            device = current;
            var sentToken = device.PushToken!;
            var stopwatch = Stopwatch.StartNew();
            var sendResult = await _firebase.SendAsync(sentToken, payload, cancellationToken);
            stopwatch.Stop();
            await AuditAttemptAsync(device, sendResult, (int)stopwatch.ElapsedMilliseconds);
            var deactivated = false;

            if (sendResult.IsInvalidToken)
            {
                // Only the token that just failed is dropped; a token the app refreshed meanwhile is kept.
                deactivated = await _devices.ClearPushTokenIfMatchesAsync(
                    device.Id, Device.ComputeTokenHash(sentToken), cancellationToken);
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

            // Re-check every device against the database at send time (see SendToDeviceAsync).
            // The payload was meant for the account the caller saw on each device; a device that now belongs to
            // anyone else is skipped.
            var intendedUsers = devices.ToDictionary(device => device.Id, device => device.UserId);
            var eligible = (await _devices.GetPushEligibleByIdsAsync(
                    intendedUsers.Keys.ToList(), DateTime.UtcNow, cancellationToken))
                .Where(fresh => intendedUsers[fresh.Id] == fresh.UserId)
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
            var stopwatch = Stopwatch.StartNew();
            var sendResults = await _firebase.SendToManyAsync(tokens, payload, cancellationToken);
            stopwatch.Stop();
            var successful = 0;
            var failed = 0;
            var deactivated = 0;

            for (var index = 0; index < eligible.Count; index++)
            {
                var device = eligible[index];
                var result = sendResults[index];
                // The batch call has one latency; it is recorded against every device in the batch.
                await AuditAttemptAsync(device, result, (int)stopwatch.ElapsedMilliseconds);

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

                if (await _devices.ClearPushTokenIfMatchesAsync(
                        device.Id, Device.ComputeTokenHash(tokens[index]), cancellationToken))
                {
                    deactivated++;
                }
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

            // A notification goes only to devices whose login session is still live. Logging out, a session being
            // evicted or expiring ends delivery to that device, even if its own flags were never switched off.
            var targets = await _devices.GetPushTargetsByUserIdAsync(userId, DateTime.UtcNow, cancellationToken);
            if (targets.Count == 0)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "No device with an active login session and a push token is available for this user.",
                    "userId");
            }

            var result = await SendToDevicesAsync(targets, payload, cancellationToken);
            return new PushUserNotificationResult
            {
                UserId = userId,
                TotalDevices = devices.Count,
                EligibleDevices = result.EligibleDevices,
                Successful = result.Successful,
                Failed = result.Failed,
                InvalidTokensDeactivated = result.InvalidTokensDeactivated
            };
        }

        private Task AuditAttemptAsync(Device device, FirebaseTokenSendResult result, int latencyMs)
        {
            // FCM only confirms it accepted the message; delivery to the handset is not reported back,
            // so an accepted push stays DeliveryStatus.Unknown.
            return _audit.WriteNotificationAsync(
                new NotificationAuditEntry(
                    NotificationChannel.Push,
                    "fcm",
                    AuditEvents.Notification.Push,
                    device.UserId,
                    device.Id,
                    null,
                    null,
                    1,
                    false,
                    result.Success ? NotificationAuditStatus.Accepted : NotificationAuditStatus.Failed,
                    result.MessageId,
                    result.Success ? null : result.ErrorCode ?? (result.IsInvalidToken ? "invalid_token" : "send_failed"),
                    result.Success ? null : result.ErrorMessage,
                    latencyMs),
                CancellationToken.None);
        }

    }
}
