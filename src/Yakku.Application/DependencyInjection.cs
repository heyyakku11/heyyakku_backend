using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yakku.Application.AdminAuth;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.AdminAuth.Services;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Services;
using Yakku.Application.Configuration;
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
            services.AddScoped<Yakku.Application.SavedCards.Interfaces.ISavedCardService, Yakku.Application.SavedCards.Services.SavedCardService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<ISessionService, SessionService>();
            services.AddScoped<ISessionCleanupService, SessionCleanupService>();
            services.AddScoped<IAdminAuthService, AdminAuthService>();
            services.AddScoped<IAdminSessionService, AdminSessionService>();
            services.AddScoped<Yakku.Application.AdminAuth.Security.AdminLoginThrottle>();
            services.AddSingleton<Yakku.Application.AdminAuth.Security.AdminCsrfTokens>();
            services.AddSingleton(TimeProvider.System);
            services.AddSingleton<IAdminAuthSettings, ConfigurationAdminAuthSettings>();
            services.AddScoped<IUserService, UserService>();
            services.AddScoped<IAccountDeletionService, AccountDeletionService>();
            services.AddScoped<IAnonymousNameAllocator, AnonymousNameAllocator>();
            services.AddScoped<IAnonymousNameService, AnonymousNameService>();
            services.AddScoped<IUserSettingsService, UserSettingsService>();
            services.AddScoped<IGuestIdentityService, GuestIdentityService>();
            services.AddScoped<IGuestMediaClaimService, GuestMediaClaimService>();
            services.AddScoped<IGuestCleanupService, GuestCleanupService>();
            services.AddScoped<IVoteService, VoteService>();
            services.AddScoped<ISharedContentService, SharedContentService>();
            services.AddScoped<IVotingService, VotingService>();
            services.AddScoped<Yakku.Application.Streaks.Interfaces.IStreakService, Yakku.Application.Streaks.Services.StreakService>();
            services.AddScoped<ICheckService, CheckService>();
            services.AddScoped<Yakku.Application.Personality.Interfaces.IPersonalityService, Yakku.Application.Personality.Services.PersonalityService>();
            services.AddScoped<Yakku.Application.Reasons.Interfaces.IReasonService, Yakku.Application.Reasons.Services.ReasonService>();
            services.AddScoped<IAdminCheckService, AdminCheckService>();
            services.AddScoped<Yakku.Application.Community.Interfaces.ICommunityWaitlistService, Yakku.Application.Community.Services.CommunityWaitlistService>();
            services.AddScoped<Yakku.Application.Community.Interfaces.ICommunityQueuedQuestionService, Yakku.Application.Community.Services.CommunityQueuedQuestionService>();
            services.AddScoped<IFunCardService, FunCardService>();
            services.AddScoped<IAdminFunCardService, AdminFunCardService>();
            services.AddScoped<Yakku.Application.Checks.Themes.ICheckThemeService, CheckThemeService>();
            services.AddScoped<Yakku.Application.AdminUsers.Interfaces.IAdminUserService, Yakku.Application.AdminUsers.Services.AdminUserService>();
            services.AddScoped<Yakku.Application.AdminPolls.Interfaces.IAdminPollService, Yakku.Application.AdminPolls.Services.AdminPollService>();
            services.AddScoped<Yakku.Application.AdminReasonReports.Interfaces.IAdminReasonReportService, Yakku.Application.AdminReasonReports.Services.AdminReasonReportService>();
            services.AddScoped<Yakku.Application.Plans.Interfaces.IPlanService, Yakku.Application.Plans.Services.PlanService>();
            services.AddScoped<IDeviceService, DeviceService>();
            services.AddScoped<IDeviceSessionBinder>(provider => (DeviceService)provider.GetRequiredService<IDeviceService>());
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<IPushNotificationService, PushNotificationService>();
            services.AddScoped<Yakku.Application.Entitlements.IEntitlementService, Yakku.Application.Entitlements.EntitlementService>();
            services.AddScoped<Yakku.Application.AskBack.Interfaces.IAskBackService, Yakku.Application.AskBack.Services.AskBackService>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentOrderService, Yakku.Application.Payments.Services.PaymentOrderService>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentVerificationService, Yakku.Application.Payments.Services.PaymentVerificationService>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentFulfillmentService, Yakku.Application.Payments.Services.PaymentFulfillmentService>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentQueryService, Yakku.Application.Payments.Services.PaymentQueryService>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentReconciliationService, Yakku.Application.Payments.Services.PaymentReconciliationService>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentRefundService, Yakku.Application.Payments.Services.PaymentRefundService>();
            services.AddSingleton<Yakku.Application.Payments.Services.RazorpayWebhookSignal>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IRazorpayWebhookReceiver, Yakku.Application.Payments.Services.RazorpayWebhookReceiver>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IRazorpayWebhookProcessor, Yakku.Application.Payments.Services.RazorpayWebhookProcessor>();
            // Infrastructure replaces this with the durable writer; tests can keep the no-op.
            services.AddSingleton<Yakku.Application.Payments.IPaymentLogWriter, Yakku.Application.Payments.NullPaymentLogWriter>();
            // The API replaces these with the SignalR-backed ones; elsewhere nothing is connected.
            services.AddSingleton<Yakku.Application.AskBack.Interfaces.IAskBackRealtime, Yakku.Application.AskBack.Interfaces.NullAskBackRealtime>();
            services.AddSingleton<Yakku.Application.AskBack.Interfaces.IAskBackPresence, Yakku.Application.AskBack.Interfaces.NullAskBackPresence>();
            services.AddSingleton<IOtpGenerator, OtpGenerator>();
            services.AddSingleton<IDisplayNameGenerator, DisplayNameGenerator>();
            services.AddSingleton<IGuestTokenGenerator, GuestTokenGenerator>();
            services.AddSingleton<IGuestTokenHasher, GuestTokenHasher>();
            services.AddSingleton<IGuestSessionTokenFactory>(sp =>
            {
                var secret = sp.GetRequiredService<IOptions<JwtSettings>>().Value.Secret?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(secret))
                {
                    throw new InvalidOperationException(
                        "Jwt:Secret is not set. Add JWT_SECRET to your .env file or environment variables.");
                }

                return new GuestSessionTokenFactory(secret);
            });
            services.AddSingleton<ITokenService>(sp =>
            {
                var secret = sp.GetRequiredService<IOptions<JwtSettings>>().Value.Secret?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(secret))
                {
                    throw new InvalidOperationException(
                        "Jwt:Secret is not set. Add JWT_SECRET to your .env file or environment variables.");
                }

                return new TokenService(secret);
            });
            return services;
        }
    }
}
