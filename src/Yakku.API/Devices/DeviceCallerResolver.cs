using Yakku.API.Auth;
using Yakku.API.Media;
using Yakku.Application.Devices;

namespace Yakku.API.Devices
{
    /// <summary>
    /// Builds the <see cref="DeviceCaller"/> from credentials the server has already validated. Ownership comes
    /// only from the JWT or the guest token, never from the request body.
    /// </summary>
    public sealed class DeviceCallerResolver
    {
        /// <summary>Identifies the installation for guests (who have no login session) and overrides the session link for users.</summary>
        public const string InstallationIdHeaderName = "X-Installation-Id";

        private readonly MediaCallerResolver _mediaCallers;

        public DeviceCallerResolver(MediaCallerResolver mediaCallers)
        {
            _mediaCallers = mediaCallers;
        }

        /// <summary>A signed-in user, or else a valid unexpired guest. Anything else is a 401.</summary>
        public async Task<DeviceCaller> ResolveAsync(HttpContext httpContext, CancellationToken cancellationToken)
        {
            if (httpContext.User.TryGetUserId(out var userId))
            {
                return DeviceCaller.ForUser(
                    userId,
                    httpContext.User.GetRequiredSessionId(),
                    MediaCallerResolver.ReadGuestToken(httpContext));
            }

            var owner = await _mediaCallers.ResolveRequiredAsync(httpContext, cancellationToken);
            return DeviceCaller.ForGuest(owner.GuestId!.Value);
        }

        /// <summary>A signed-in user only (guests have no device list).</summary>
        public DeviceCaller ResolveUser(HttpContext httpContext)
        {
            return DeviceCaller.ForUser(
                httpContext.User.GetRequiredUserId(),
                httpContext.User.GetRequiredSessionId());
        }

        public static string? ReadInstallationId(HttpContext httpContext)
        {
            return httpContext.Request.Headers.TryGetValue(InstallationIdHeaderName, out var values)
                ? values.FirstOrDefault()
                : null;
        }
    }
}
