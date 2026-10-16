using Yakku.API.Media;
using Yakku.Application.Guests.DTOs;
using Yakku.Application.Guests.Interfaces;

namespace Yakku.API.Guests
{
    public class GuestCookieService
    {
        public const string CookieName = "yakku_guest";

        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

        private readonly IGuestIdentityService _guestIdentityService;

        public GuestCookieService(IGuestIdentityService guestIdentityService)
        {
            _guestIdentityService = guestIdentityService;
        }

        public async Task<GuestEstablishResult> EnsureAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken = default)
        {
            // Same lookup order as the media endpoints (X-Guest-Token header, then cookie), so a client that
            // keeps its token in app storage is recognised and not issued a second identity.
            var rawToken = MediaCallerResolver.ReadGuestToken(httpContext);
            var result = await _guestIdentityService.EstablishAsync(rawToken, cancellationToken);

            if (result.RawTokenToSet is not null)
            {
                httpContext.Response.Cookies.Append(
                    CookieName,
                    result.RawTokenToSet,
                    CreateOptions(httpContext.Request));
            }

            return result;
        }

        public void Clear(HttpContext httpContext)
        {
            httpContext.Response.Cookies.Delete(
                CookieName,
                new CookieOptions
                {
                    Path = "/",
                    Secure = httpContext.Request.IsHttps,
                    SameSite = SameSiteMode.Lax,
                    HttpOnly = true
                });
        }

        internal static CookieOptions CreateOptions(HttpRequest request)
        {
            return new CookieOptions
            {
                HttpOnly = true,
                Secure = request.IsHttps,
                SameSite = SameSiteMode.Lax,
                MaxAge = Lifetime,
                Path = "/",
                IsEssential = true
            };
        }
    }
}
