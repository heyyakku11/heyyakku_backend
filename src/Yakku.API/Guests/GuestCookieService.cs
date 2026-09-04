using Yakku.Application.Guests.Interfaces;

namespace Yakku.API.Guests
{
    public class GuestCookieService
    {
        public const string CookieName = "yakku_guest";

        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

        private readonly IGuestIdentityService _guestIdentityService;

        public GuestCookieService(IGuestIdentityService guestIdentityService)
        {
            _guestIdentityService = guestIdentityService;
        }

        public async Task<Guid> EnsureAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            httpContext.Request.Cookies.TryGetValue(CookieName, out var rawToken);
            var result = await _guestIdentityService.EstablishAsync(rawToken, cancellationToken);

            if (result.RawTokenToSet is not null)
            {
                httpContext.Response.Cookies.Append(
                    CookieName,
                    result.RawTokenToSet,
                    CreateOptions(httpContext.Request));
            }

            return result.GuestId;
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
