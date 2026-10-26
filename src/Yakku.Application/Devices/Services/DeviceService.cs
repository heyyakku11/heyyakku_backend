using FluentValidation;
using Yakku.Application.Audit;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Devices.DTOs;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.Devices.Mapper;
using Yakku.Application.Guests.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Devices.Services
{
    public class DeviceService : IDeviceService, IDeviceSessionBinder
    {
        private readonly IDeviceRepository _devices;
        private readonly IUserSessionRepository _sessions;
        private readonly IGuestRepository _guests;
        private readonly IGuestTokenHasher _guestTokenHasher;
        private readonly IValidator<RegisterDeviceRequest> _registerValidator;
        private readonly IValidator<UpdateDeviceRequest> _updateValidator;
        private readonly IAuditLogWriter _audit;

        public DeviceService(
            IDeviceRepository devices,
            IUserSessionRepository sessions,
            IGuestRepository guests,
            IGuestTokenHasher guestTokenHasher,
            IValidator<RegisterDeviceRequest> registerValidator,
            IValidator<UpdateDeviceRequest> updateValidator,
            IAuditLogWriter audit)
        {
            _devices = devices;
            _sessions = sessions;
            _guests = guests;
            _guestTokenHasher = guestTokenHasher;
            _registerValidator = registerValidator;
            _updateValidator = updateValidator;
            _audit = audit;
        }

        public async Task<RegisterDeviceResult> RegisterAsync(
            DeviceCaller caller,
            RegisterDeviceRequest request,
            CancellationToken cancellationToken = default)
        {
            await _registerValidator.ValidateAndThrowAsync(request, cancellationToken);
            await ValidateCallerAsync(caller, cancellationToken);

            var installationId = request.InstallationId.Trim();
            DeviceFieldParser.TryParsePlatform(request.Platform, out var platform);
            var permission = ParseOptionalPermission(request.NotificationPermission);

            RegisterDeviceResult? result = null;
            try
            {
                for (var attempt = 0; result is null; attempt++)
                {
                    try
                    {
                        await _devices.ExecuteInTransactionAsync(async transactionToken =>
                        {
                            result = await UpsertAsync(
                                caller, installationId, platform, permission, request, transactionToken);
                        }, cancellationToken);
                    }
                    catch (AppException exception)
                        when (attempt == 0
                              && exception is not DeviceOwnershipException
                              && exception.ErrorCode == ApiErrorCodes.DeviceConflict)
                    {
                        // Lost a unique-index race with a concurrent request for the same installation or token.
                        // The transaction is rolled back; run once more so the winner's row is updated, not duplicated.
                    }
                }
            }
            catch (DeviceOwnershipException exception)
            {
                await AuditAsync(
                    caller,
                    AuditEvents.Device.OwnershipConflict,
                    AuditOutcome.Denied,
                    exception.Reason,
                    new Dictionary<string, string?> { ["platform"] = platform.ToString().ToLowerInvariant() });
                throw;
            }

            await AuditAsync(
                caller,
                AuditEvents.Device.Registered,
                AuditOutcome.Success,
                null,
                new Dictionary<string, string?>
                {
                    ["platform"] = platform.ToString().ToLowerInvariant(),
                    ["created"] = result!.Created ? "true" : "false",
                    ["pushEnabled"] = result.Device.PushEnabled ? "true" : "false"
                });
            return result;
        }

        public async Task<DeviceResponse> GetCurrentAsync(
            DeviceCaller caller,
            string? installationId,
            CancellationToken cancellationToken = default)
        {
            var session = await ValidateCallerAsync(caller, cancellationToken);
            var device = await ResolveCurrentAsync(caller, session, installationId, cancellationToken)
                ?? throw DeviceNotFound();
            EnsureAssociatedWithCaller(device, caller);
            return device.ToResponse(isCurrent: true);
        }

        public async Task<DeviceResponse> UpdateCurrentAsync(
            DeviceCaller caller,
            string? installationId,
            UpdateDeviceRequest request,
            CancellationToken cancellationToken = default)
        {
            await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);
            var session = await ValidateCallerAsync(caller, cancellationToken);
            var permission = ParseOptionalPermission(request.NotificationPermission);

            DeviceResponse? response = null;
            var changed = false;
            try
            {
                await _devices.ExecuteInTransactionAsync(async transactionToken =>
                {
                    var device = await ResolveCurrentAsync(caller, session, installationId, transactionToken)
                        ?? throw DeviceNotFound();
                    EnsureAssociatedWithCaller(device, caller);

                    var effectivePermission = permission ?? device.NotificationPermission;
                    var wantsToken = !string.IsNullOrWhiteSpace(request.PushToken)
                        && effectivePermission != NotificationPermissionStatus.Denied;
                    if (wantsToken && !device.IsActive)
                    {
                        throw new AppException(
                            409,
                            ApiErrorCodes.DeviceConflict,
                            "This device is unregistered. Register it again before setting a push token.",
                            "pushToken");
                    }

                    if (wantsToken)
                    {
                        await ReleaseTokenFromOthersAsync(
                            request.PushToken!.Trim(), device.Id, caller, transactionToken);
                    }

                    changed = device.ApplyUpdate(
                        request.PushToken,
                        request.DeviceModel,
                        request.OsVersion,
                        request.AppVersion,
                        request.AppBuild,
                        request.Locale,
                        request.Timezone,
                        permission,
                        request.IsPushAllowed);
                    if (changed)
                    {
                        await _devices.SaveChangesAsync(transactionToken);
                    }

                    response = device.ToResponse(isCurrent: true);
                }, cancellationToken);
            }
            catch (DeviceOwnershipException exception)
            {
                await AuditAsync(
                    caller, AuditEvents.Device.OwnershipConflict, AuditOutcome.Denied, exception.Reason, null);
                throw;
            }

            if (changed)
            {
                await AuditAsync(
                    caller,
                    AuditEvents.Device.Updated,
                    AuditOutcome.Success,
                    null,
                    new Dictionary<string, string?> { ["pushEnabled"] = response!.PushEnabled ? "true" : "false" });
            }

            return response!;
        }

        public async Task UnregisterCurrentAsync(
            DeviceCaller caller,
            string? installationId,
            CancellationToken cancellationToken = default)
        {
            var session = await ValidateCallerAsync(caller, cancellationToken);
            var changed = false;

            await _devices.ExecuteInTransactionAsync(async transactionToken =>
            {
                var device = await ResolveCurrentAsync(caller, session, installationId, transactionToken)
                    ?? throw DeviceNotFound();
                EnsureAssociatedWithCaller(device, caller);

                if (caller.IsUser)
                {
                    // Conditional on the caller's own session still being the installation's current one, so a
                    // delayed unregister cannot switch off an installation that has since moved to another session.
                    var wasUserEnabled = device.IsActive || device.PushToken is not null;
                    var affected = await _devices.DeactivateBySessionAsync(caller.SessionId!.Value, transactionToken);
                    changed = wasUserEnabled && affected > 0;
                    return;
                }

                var wasEnabled = device.IsActive || device.PushToken is not null;
                device.Deactivate();
                if (wasEnabled)
                {
                    await _devices.SaveChangesAsync(transactionToken);
                    changed = true;
                }
            }, cancellationToken);

            if (changed)
            {
                await AuditAsync(caller, AuditEvents.Device.Unregistered, AuditOutcome.Success, null, null);
            }
        }

        public async Task<IReadOnlyList<DeviceResponse>> ListAsync(
            DeviceCaller caller,
            string? currentInstallationId,
            CancellationToken cancellationToken = default)
        {
            if (!caller.IsUser)
            {
                throw Unauthorized();
            }

            var session = await ValidateCallerAsync(caller, cancellationToken);
            var currentId = string.IsNullOrWhiteSpace(currentInstallationId)
                ? null
                : currentInstallationId.Trim();
            var devices = await _devices.GetByUserIdAsync(caller.UserId!.Value, cancellationToken);

            return devices
                .Select(device => device.ToResponse(
                    isCurrent: currentId is not null
                        ? string.Equals(device.InstallationId, currentId, StringComparison.Ordinal)
                        : session?.DeviceId == device.Id))
                .ToList();
        }

        private async Task<RegisterDeviceResult> UpsertAsync(
            DeviceCaller caller,
            string installationId,
            DevicePlatform platform,
            NotificationPermissionStatus? permission,
            RegisterDeviceRequest request,
            CancellationToken cancellationToken)
        {
            // Lock the installation row so register, login and logout on it are serialized.
            var existing = await _devices.GetByInstallationIdAndPlatformForUpdateAsync(
                installationId, platform, cancellationToken);
            var provenGuestId = await ResolveProvenGuestIdAsync(caller, cancellationToken);

            if (caller.IsUser
                && !await _sessions.IsLiveAsync(
                    caller.SessionId!.Value, caller.UserId!.Value, DateTime.UtcNow, cancellationToken))
            {
                // Logged out (or evicted/expired) after the request was authenticated: never associate it.
                throw Unauthorized();
            }

            if (existing is not null)
            {
                await EnsureCanTakeOverAsync(existing, caller, provenGuestId, cancellationToken);
            }

            var storedToken = permission == NotificationPermissionStatus.Denied
                ? null
                : NormalizeToken(request.PushToken);
            if (storedToken is not null)
            {
                await ReleaseTokenFromOthersAsync(storedToken, existing?.Id, caller, cancellationToken);
            }

            var created = existing is null;
            var device = existing ?? (caller.IsUser
                ? new Device(installationId, platform, caller.UserId)
                : Device.ForGuest(installationId, platform, caller.GuestId!.Value));

            if (caller.IsUser)
            {
                // Re-read inside the transaction: a rolled-back first attempt detaches earlier entities.
                var session = await _sessions.GetByIdAsync(caller.SessionId!.Value, cancellationToken)
                    ?? throw Unauthorized();
                if (session.DeviceId is Guid boundDeviceId && boundDeviceId != device.Id)
                {
                    throw new AppException(
                        409,
                        ApiErrorCodes.DeviceConflict,
                        "This session belongs to another installation. Sign in again on this installation.",
                        "installationId");
                }

                device.Register(
                    caller.UserId!.Value, platform, request.PushToken, request.DeviceModel, request.OsVersion,
                    request.AppVersion, request.AppBuild, request.Locale, request.Timezone, permission,
                    session.Id);
                session.AssignDevice(device.Id);
            }
            else
            {
                device.RegisterForGuest(
                    caller.GuestId!.Value, platform, request.PushToken, request.DeviceModel, request.OsVersion,
                    request.AppVersion, request.AppBuild, request.Locale, request.Timezone, permission);
            }

            if (created)
            {
                await _devices.AddAsync(device, cancellationToken);
            }

            await _devices.SaveChangesAsync(cancellationToken);
            return new RegisterDeviceResult { Device = device.ToResponse(), Created = created };
        }

        /// <summary>
        /// Decides whether the caller may become the owner of an installation that already exists. Anything that
        /// is not clearly released by its previous owner is refused rather than silently taken over.
        /// </summary>
        private async Task EnsureCanTakeOverAsync(
            Device existing,
            DeviceCaller caller,
            Guid? provenGuestId,
            CancellationToken cancellationToken)
        {
            if (IsOwnedBy(existing, caller, provenGuestId))
            {
                return;
            }

            if (existing.UserId is not null)
            {
                // Another account keeps the installation while its current session is live. After logout (or once
                // that session ends) it is free; an authenticated register still cannot evict a live account:
                // only a fresh OTP login can (see BindLoginAsync).
                if (await HasLiveCurrentSessionAsync(existing, cancellationToken))
                {
                    throw new DeviceOwnershipException(
                        "installation_owned_by_another_account",
                        "This installation is registered to another account.");
                }

                return;
            }

            if (existing.GuestId is Guid ownerGuestId)
            {
                var ownerGuest = await _guests.GetByIdAsync(ownerGuestId, cancellationToken);
                if (ownerGuest is null || ownerGuest.IsExpired(DateTime.UtcNow))
                {
                    return;
                }

                throw new DeviceOwnershipException(
                    "installation_owned_by_another_guest",
                    "This installation is registered to another session.");
            }
        }

        /// <summary>
        /// A push token belongs to one installation. A stale copy on another installation of the same owner is
        /// cleared; a copy held by a different owner is a conflict, never a takeover.
        /// </summary>
        private async Task ReleaseTokenFromOthersAsync(
            string token,
            Guid? selfId,
            DeviceCaller caller,
            CancellationToken cancellationToken)
        {
            var provenGuestId = await ResolveProvenGuestIdAsync(caller, cancellationToken);
            var others = (await _devices.GetByPushTokenHashAsync(Device.ComputeTokenHash(token), cancellationToken))
                .Where(device => device.Id != selfId)
                .ToList();
            if (others.Count == 0)
            {
                return;
            }

            foreach (var other in others)
            {
                // A holder with no live owner is stale (logged out, expired, reinstalled app); a token is never
                // taken from a device that still has a live owner of its own.
                if (!IsOwnedBy(other, caller, provenGuestId) && !await IsStaleHolderAsync(other, cancellationToken))
                {
                    throw new DeviceOwnershipException(
                        "push_token_owned_by_another_device",
                        "This push token is already registered to another device.",
                        "pushToken");
                }

                other.ClearPushToken();
            }

            // Persist the release first so the unique index never sees two holders at once.
            await _devices.SaveChangesAsync(cancellationToken);
        }

        private async Task<Device?> ResolveCurrentAsync(
            DeviceCaller caller,
            UserSession? session,
            string? installationId,
            CancellationToken cancellationToken)
        {
            if (!string.IsNullOrWhiteSpace(installationId))
            {
                var id = installationId.Trim();
                if (id.Length > 100 || !id.All(c => c is >= '!' and <= '~'))
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "The installation id is invalid.",
                        "installationId");
                }

                return await _devices.GetOwnedByInstallationIdAsync(
                    caller.UserId, caller.GuestId, id, cancellationToken);
            }

            if (!caller.IsUser)
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "The X-Installation-Id header is required.",
                    "installationId");
            }

            if (session?.DeviceId is not Guid deviceId)
            {
                return null;
            }

            var device = await _devices.GetByIdAsync(deviceId, cancellationToken);
            return device?.UserId == caller.UserId ? device : null;
        }

        /// <summary>Validates the caller and, for users, that their login session is still live.</summary>
        private async Task<UserSession?> ValidateCallerAsync(DeviceCaller caller, CancellationToken cancellationToken)
        {
            if (caller.UserId is null && caller.GuestId is null)
            {
                throw Unauthorized();
            }

            if (caller.UserId is not Guid userId)
            {
                return null;
            }

            if (caller.SessionId is not Guid sessionId || sessionId == Guid.Empty)
            {
                throw Unauthorized();
            }

            var session = await _sessions.GetByIdAsync(sessionId, cancellationToken);
            var utcNow = DateTime.UtcNow;
            if (session is null
                || session.UserId != userId
                || session.RevokedAt is not null
                || session.IsExpired(utcNow))
            {
                throw new AppException(401, ApiErrorCodes.Unauthorized, "Invalid or expired session.");
            }

            return session;
        }

        /// <summary>The guest whose token the signed-in user also presented, if it is valid and unexpired.</summary>
        private async Task<Guid?> ResolveProvenGuestIdAsync(DeviceCaller caller, CancellationToken cancellationToken)
        {
            if (!caller.IsUser || string.IsNullOrWhiteSpace(caller.PresentedGuestToken))
            {
                return null;
            }

            var guest = await _guests.GetByTokenHashAsync(
                _guestTokenHasher.Hash(caller.PresentedGuestToken.Trim()),
                cancellationToken);
            return guest is not null && !guest.IsExpired(DateTime.UtcNow) ? guest.Id : null;
        }

        private static bool IsOwnedBy(Device device, DeviceCaller caller, Guid? provenGuestId)
        {
            return (caller.UserId is not null && device.UserId == caller.UserId)
                || (caller.GuestId is not null && device.GuestId == caller.GuestId)
                || (provenGuestId is not null && device.GuestId == provenGuestId);
        }

        public async Task<DeviceLoginBindResult> BindLoginAsync(
            Guid userId,
            UserSession session,
            RegisterDeviceRequest installation,
            CancellationToken cancellationToken = default)
        {
            await _registerValidator.ValidateAndThrowAsync(installation, cancellationToken);

            var installationId = installation.InstallationId.Trim();
            DeviceFieldParser.TryParsePlatform(installation.Platform, out var platform);
            var permission = ParseOptionalPermission(installation.NotificationPermission);
            var caller = DeviceCaller.ForUser(userId, session.Id);

            var existing = await _devices.GetByInstallationIdAndPlatformForUpdateAsync(
                installationId, platform, cancellationToken);

            Guid? displacedSessionId = null;
            Guid? previousUserId = null;
            if (existing is not null)
            {
                if (existing.GuestId is not null)
                {
                    // A guest's installation moves to the account through the guest-claim step, not here.
                    throw new DeviceOwnershipException(
                        "installation_owned_by_guest",
                        "This installation is registered to a guest session.");
                }

                if (existing.CurrentSessionId is Guid previousSessionId && previousSessionId != session.Id)
                {
                    // One active account per installation: the session that used to own it ends. Its user and
                    // device stay on the row as history; nothing about that session is rewritten.
                    var previous = await _sessions.GetByIdAsync(previousSessionId, cancellationToken);
                    if (previous is not null)
                    {
                        previousUserId = previous.UserId;
                        previous.Revoke();
                        displacedSessionId = previous.Id;
                    }
                }
            }

            var storedToken = permission == NotificationPermissionStatus.Denied
                ? null
                : NormalizeToken(installation.PushToken);
            if (storedToken is not null)
            {
                await ReleaseTokenFromOthersAsync(storedToken, existing?.Id, caller, cancellationToken);
            }

            var created = existing is null;
            var device = existing ?? new Device(installationId, platform, userId);
            device.Register(
                userId, platform, installation.PushToken, installation.DeviceModel, installation.OsVersion,
                installation.AppVersion, installation.AppBuild, installation.Locale, installation.Timezone,
                permission, session.Id);
            session.AssignDevice(device.Id);

            if (created)
            {
                await _devices.AddAsync(device, cancellationToken);
            }

            await _devices.SaveChangesAsync(cancellationToken);

            if (displacedSessionId is not null && previousUserId != userId)
            {
                await AuditAsync(
                    caller,
                    AuditEvents.Device.AccountSwitched,
                    AuditOutcome.Success,
                    null,
                    new Dictionary<string, string?> { ["displacedSessionId"] = displacedSessionId.ToString() });
            }

            return new DeviceLoginBindResult(device.Id, displacedSessionId, previousUserId);
        }

        /// <summary>Installations are used by the session that owns them, not by any session of the same user.</summary>
        private static void EnsureAssociatedWithCaller(Device device, DeviceCaller caller)
        {
            if (caller.IsUser && device.CurrentSessionId is Guid current && current != caller.SessionId)
            {
                throw DeviceNotFound();
            }
        }

        private async Task<bool> HasLiveCurrentSessionAsync(Device device, CancellationToken cancellationToken)
        {
            return device is { CurrentSessionId: Guid sessionId, UserId: Guid userId }
                && await _sessions.IsLiveAsync(sessionId, userId, DateTime.UtcNow, cancellationToken);
        }

        /// <summary>True when nobody live owns the device: no live session for a user, an expired or gone guest.</summary>
        private async Task<bool> IsStaleHolderAsync(Device holder, CancellationToken cancellationToken)
        {
            if (holder.UserId is not null)
            {
                return !await HasLiveCurrentSessionAsync(holder, cancellationToken);
            }

            if (holder.GuestId is Guid guestId)
            {
                var guest = await _guests.GetByIdAsync(guestId, cancellationToken);
                return guest is null || guest.IsExpired(DateTime.UtcNow);
            }

            return true;
        }

        private Task AuditAsync(
            DeviceCaller caller,
            string eventType,
            AuditOutcome outcome,
            string? reason,
            IReadOnlyDictionary<string, string?>? metadata)
        {
            return _audit.WriteAuthenticationAsync(
                new AuthenticationAuditEntry(
                    caller.IsUser ? AuditActorType.User : AuditActorType.Guest,
                    caller.UserId ?? caller.GuestId,
                    eventType,
                    outcome,
                    reason,
                    SessionId: caller.SessionId,
                    Metadata: metadata),
                CancellationToken.None);
        }

        private static string? NormalizeToken(string? token)
        {
            return string.IsNullOrWhiteSpace(token) ? null : token.Trim();
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

        private static AppException Unauthorized()
        {
            return new AppException(401, ApiErrorCodes.Unauthorized, "Unauthorized.");
        }

        private static AppException DeviceNotFound()
        {
            // Same answer for "does not exist" and "belongs to someone else": ownership is never revealed.
            return new AppException(404, ApiErrorCodes.NotFound, "Device not found.", "installationId");
        }

        /// <summary>Refusal to take over an installation or token that belongs to another owner.</summary>
        private sealed class DeviceOwnershipException : AppException
        {
            public DeviceOwnershipException(string reason, string message, string field = "installationId")
                : base(409, ApiErrorCodes.DeviceConflict, message, field)
            {
                Reason = reason;
            }

            public string Reason { get; }
        }
    }
}
