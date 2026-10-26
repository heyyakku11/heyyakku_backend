using Yakku.Application.Devices.DTOs;
using Yakku.Domain.Entities;

namespace Yakku.Application.Devices.Interfaces
{
    /// <summary>Outcome of binding an installation to a freshly created login session.</summary>
    public sealed record DeviceLoginBindResult(Guid DeviceId, Guid? DisplacedSessionId, Guid? PreviousUserId);

    /// <summary>
    /// Associates an installation with the login session that was just created. The caller has just proven control
    /// of the account (OTP), which is what authorizes taking the installation over; the installation id and push
    /// token are never proof of identity.
    /// </summary>
    public interface IDeviceSessionBinder
    {
        /// <summary>
        /// Must run inside the login transaction, after <paramref name="session"/> has been saved. Locks the
        /// installation row, revokes the session it was previously associated with (if any), points the installation
        /// at the new user and session, and links the new session to the installation.
        /// </summary>
        Task<DeviceLoginBindResult> BindLoginAsync(
            Guid userId,
            UserSession session,
            RegisterDeviceRequest installation,
            CancellationToken cancellationToken = default);
    }
}
