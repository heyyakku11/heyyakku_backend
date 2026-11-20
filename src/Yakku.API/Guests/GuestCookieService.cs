using System.Diagnostics;
using Yakku.API.Media;
using Yakku.Application.Guests.DTOs;
using Yakku.Application.Guests.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.API.Guests
{
    public class GuestCookieService
    {
        public const string CookieName = "yakku_guest";

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
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew(); //remove later
            // Same lookup order as the media endpoints (X-Guest-Token header, then cookie), so a client that
            // keeps its token in app storage is recognised and not issued a second identity.
            var rawToken = MediaCallerResolver.ReadGuestToken(httpContext);
            LogStep("read guest token from request", stopwatch);
            var client = GuestClientInfoReader.From(httpContext);
            LogStep("read client info from headers", stopwatch);
            var result = await _guestIdentityService.EstablishAsync(
                rawToken,
                idempotencyKey,
                client,
                cancellationToken);
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
                MaxAge = Guest.Lifetime,
                Path = "/",
                IsEssential = true
            };
        }
    }
}
