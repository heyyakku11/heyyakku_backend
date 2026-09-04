using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Services;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Guests.Services;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Services;
using Yakku.Application.Users.Interfaces;
using Yakku.Application.Users.Services;
using Yakku.Application.System.Interfaces;
using Yakku.Application.System.Services;
using Yakku.Application.Votes.Interfaces;
using Yakku.Application.Votes.Services;

namespace Yakku.Application
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddApplication(this IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
            services.AddScoped<IPollService, PollService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<ISystemService, SystemService>();
            services.AddScoped<IGuestIdentityService, GuestIdentityService>();
            services.AddScoped<IVoteService, VoteService>();
            services.AddSingleton<IOtpGenerator, OtpGenerator>();
            services.AddSingleton<IDisplayNameGenerator, DisplayNameGenerator>();
            services.AddSingleton<IGuestTokenGenerator, GuestTokenGenerator>();
            services.AddSingleton<IGuestTokenHasher, GuestTokenHasher>();
            services.AddSingleton<ITokenService>(_ =>
            {
                var secret = Environment.GetEnvironmentVariable("JWT_SECRET")?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(secret))
                {
                    throw new InvalidOperationException(
                        "JWT_SECRET is not set. Add it to your .env file or environment variables.");
                }

                return new TokenService(secret);
            });
            return services;
        }
    }
}
