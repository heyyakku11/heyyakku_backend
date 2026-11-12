using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Yakku.Application.AdminAuth.Security;
using Yakku.Application.Auth;

namespace Yakku.API.AdminSecurity
{
    /// <summary>One place that wires admin security, shared by the app and the HTTP tests so they cannot drift apart.</summary>
    public static class AdminSecurityExtensions
    {
        public const string AdminPolicy = "Admin";

        public static IServiceCollection AddAdminSecurity(this IServiceCollection services, AdminSecuritySettings settings)
        {
            services.AddSingleton(settings);
            services.AddSingleton(settings.Session);
            services.AddSingleton<AdminCsrfTokens>();
            services.AddSingleton<AdminCookieService>();

            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, AdminCookieAuthenticationHandler>(
                    AdminSecuritySettings.AuthenticationScheme,
                    _ => { });

            services.AddAuthorization(options =>
            {
                // Only the cookie scheme can satisfy this policy: a JWT never authenticates an admin route.
                options.AddPolicy(AdminPolicy, policy => policy
                    .AddAuthenticationSchemes(AdminSecuritySettings.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireClaim(AuthClaimTypes.Actor, AuthClaimTypes.AdminActor));
            });

            return services;
        }

        /// <summary>
        /// Admins authenticate with the session cookie only. A bearer token that claims to be an admin (left over
        /// from the old login) is refused instead of being accepted as a second way in.
        /// </summary>
        public static Task RejectAdminBearer(TokenValidatedContext context)
        {
            if (context.Principal?.HasClaim(AuthClaimTypes.Actor, AuthClaimTypes.AdminActor) == true)
            {
                context.Fail("Admin bearer tokens are no longer accepted.");
            }

            return Task.CompletedTask;
        }
    }
}
