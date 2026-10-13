using Yakku.API.Auth;
using Yakku.API.Guests;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Polls;

namespace Yakku.API.Media
{
    public sealed class MediaCallerResolver
    {
        public const string GuestTokenHeaderName = "X-Guest-Token";

        private readonly IGuestRepository _guests;
        private readonly IGuestTokenHasher _tokenHasher;

        public MediaCallerResolver(IGuestRepository guests, IGuestTokenHasher tokenHasher)
        {
            _guests = guests;
            _tokenHasher = tokenHasher;
        }

        public async Task<MediaOwner> ResolveRequiredAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken = default)
        {
            if (httpContext.User.TryGetUserId(out var userId))
            {
                return MediaOwner.ForUser(userId);
            }

            var rawToken = ReadGuestToken(httpContext);
            if (string.IsNullOrWhiteSpace(rawToken))
            {
                throw new AppException(
                    StatusCodes.Status401Unauthorized,
                    ApiErrorCodes.Unauthorized,
                    "Authentication is required.");
            }

            var guest = await _guests.GetByTokenHashAsync(_tokenHasher.Hash(rawToken), cancellationToken);
            if (guest is null || guest.ExpiresAt <= DateTime.UtcNow)
            {
                throw new AppException(
                    StatusCodes.Status401Unauthorized,
                    ApiErrorCodes.Unauthorized,
                    "Guest session is invalid or expired.");
            }

            guest.Touch();
            await _guests.SaveChangesAsync(cancellationToken);
            return MediaOwner.ForGuest(guest.Id);
        }

        public static string? ReadGuestToken(HttpContext httpContext)
        {
            if (httpContext.Request.Headers.TryGetValue(GuestTokenHeaderName, out var headerValues))
            {
                var header = headerValues.FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(header))
                {
                    return header.Trim();
                }
            }

            if (httpContext.Request.Cookies.TryGetValue(GuestCookieService.CookieName, out var cookie)
                && !string.IsNullOrWhiteSpace(cookie))
            {
                return cookie;
            }

            return null;
        }
    }
}
