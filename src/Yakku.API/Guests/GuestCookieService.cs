using System.Diagnostics;
using Yakku.API.Media;
using Yakku.Application.Guests.DTOs;
using Yakku.Application.Guests.Interfaces;

namespace Yakku.API.Guests
{
    public class GuestCookieService
    {
        public const string CookieName = "yakku_guest";

        private static readonly TimeSpan Lifetime = TimeSpan.FromDays(30);

        private readonly IGuestIdentityService _guestIdentityService;

        private readonly ILogger<GuestCookieService>? _logger;

        public GuestCookieService(
            IGuestIdentityService guestIdentityService,
            ILogger<GuestCookieService>? logger = null)
        {
            _guestIdentityService = guestIdentityService;
            _logger = logger;
        }

        public async Task<GuestEstablishResult> EnsureAsync(
            HttpContext httpContext,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew(); //remove later
            // Same lookup order as the media endpoints (X-Guest-Token header, then cookie), so a client that
            // keeps its token in app storage is recognised and not issued a second identity.
            var rawToken = MediaCallerResolver.ReadGuestToken(httpContext);
            LogStep("read guest token from request", stopwatch);
            var result = await _guestIdentityService.EstablishAsync(rawToken, cancellationToken);
            LogStep("establish guest identity (total)", stopwatch);

            if (result.RawTokenToSet is not null)
            {
                httpContext.Response.Cookies.Append(
                    CookieName,
                    result.RawTokenToSet,
                    CreateOptions(httpContext.Request));
                LogStep("set guest cookie", stopwatch);
            }

            return result;
        }

        private void LogStep(string step, Stopwatch stopwatch)  //remove later
        {
            _logger?.LogInformation("guest timing | {Step} took {ElapsedMs} ms", step, stopwatch.Elapsed.TotalMilliseconds);
            stopwatch.Restart();
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
