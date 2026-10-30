using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Yakku.Application.AdminAuth;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.AdminAuth.Services;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Services;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Checks.Services;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.Devices.Services;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Guests.Services;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.Notifications.Services;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.PushNotifications.Services;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Services;
using Yakku.Application.Sharing.Interfaces;
using Yakku.Application.Sharing.Services;
using Yakku.Application.Users.Interfaces;
using Yakku.Application.Users.Services;
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
            services.AddScoped<IMediaUploadSessionService, MediaUploadSessionService>();
            services.AddScoped<IMediaUploadCleanupService, MediaUploadCleanupService>();
            services.AddScoped<ISavedPollService, SavedPollService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<ISessionCleanupService, SessionCleanupService>();
            services.AddScoped<IAdminAuthService, AdminAuthService>();
            services.AddScoped<IAdminSessionService, AdminSessionService>();
            services.AddSingleton<IAdminAuthSettings, EnvAdminAuthSettings>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAccountDeletionService, AccountDeletionService>();
            services.AddScoped<IGuestIdentityService, GuestIdentityService>();
            services.AddScoped<IGuestMediaClaimService, GuestMediaClaimService>();
            services.AddScoped<IGuestCleanupService, GuestCleanupService>();
            services.AddScoped<IVoteService, VoteService>();
            services.AddScoped<ISharedContentService, SharedContentService>();
            services.AddScoped<IVotingService, VotingService>();
            services.AddScoped<Yakku.Application.Streaks.Interfaces.IStreakService, Yakku.Application.Streaks.Services.StreakService>();
            services.AddScoped<ICheckService, CheckService>();
            services.AddScoped<Yakku.Application.Reasons.Interfaces.IReasonService, Yakku.Application.Reasons.Services.ReasonService>();
            services.AddScoped<IAdminCheckService, AdminCheckService>();
            services.AddScoped<Yakku.Application.Plans.Interfaces.IPlanService, Yakku.Application.Plans.Services.PlanService>();
            services.AddScoped<IDeviceService, DeviceService>();
            services.AddScoped<IDeviceSessionBinder>(provider => (DeviceService)provider.GetRequiredService<IDeviceService>());
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IPushNotificationService, PushNotificationService>();
            services.AddScoped<Yakku.Application.AskBack.Interfaces.IAskBackService, Yakku.Application.AskBack.Services.AskBackService>();
            // The API replaces these with the SignalR-backed ones; elsewhere nothing is connected.
            services.AddSingleton<Yakku.Application.AskBack.Interfaces.IAskBackRealtime, Yakku.Application.AskBack.Interfaces.NullAskBackRealtime>();
            services.AddSingleton<Yakku.Application.AskBack.Interfaces.IAskBackPresence, Yakku.Application.AskBack.Interfaces.NullAskBackPresence>();
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
