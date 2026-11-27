using Yakku.API.Media;
using Yakku.API.Middleware;
using Yakku.Application.Audit;
using Yakku.Application.Guests;
using Yakku.Application.Guests.Interfaces;

namespace Yakku.API.Guests
{
    /// <summary>
    /// The guest claim shared by every sign-in route (app and web): moves the caller's guest data to the freshly
    /// authenticated user, then drops the guest cookie once the credential is useless.
    /// </summary>
    public static class GuestClaimHandler
    {
        /// <summary>
        /// Strictly optional: it can never fail the sign-in, because the OTP is already consumed and the user already
        /// exists by the time this runs.
        /// </summary>
        /// <param name="ownerName">Name recorded in the system log when the claim throws unexpectedly.</param>
        public static async Task<GuestClaimResult> ClaimAsync(
            HttpContext httpContext,
            IGuestMediaClaimService guestMediaClaim,
            GuestCookieService guestCookies,
            ILogger logger,
            Guid userId,
            string ownerName,
            CancellationToken cancellationToken)
        {
            var guestToken = MediaCallerResolver.ReadGuestToken(httpContext);
            if (string.IsNullOrWhiteSpace(guestToken))
            {
                return GuestClaimResult.NoToken;
            }

            GuestClaimResult outcome;
            try
            {
                outcome = await guestMediaClaim.TryClaimAsync(guestToken, userId, cancellationToken);
            }
            catch (Exception exception)
            {
                // TryClaimAsync is written never to throw; this is the backstop so that can never become a 500.
                logger.LogError(
                    "Guest claim threw unexpectedly ({ExceptionType}); sign-in continues.",
                    exception.GetType().Name);
                var audit = httpContext.RequestServices?.GetService<IAuditLogWriter>();
                if (audit is not null)
                {
                    _ = ExceptionSystemLog.WriteAsync(
                        audit,
                        exception,
                        ownerName,
                        httpContext.Request.Method,
                        httpContext.Request.Path.Value,
                        StatusCodes.Status500InternalServerError,
                        extraMetadata: new Dictionary<string, string?>
                        {
                            ["stage"] = "guest_claim",
                            ["userId"] = userId.ToString("D")
                        },
                        cancellationToken: CancellationToken.None);
                }

                outcome = GuestClaimResult.Failed;
            }

            logger.LogInformation("Guest claim finished with outcome {Outcome}.", outcome);

            // Claimed, unknown and expired tokens are useless from now on and are dropped. After a failure the
            // guest (rolled back, intact) and its credential are kept so the next sign-in can retry.
            if (outcome != GuestClaimResult.Failed)
            {
                guestCookies.Clear(httpContext);
            }

            return outcome;
        }
    }
}
