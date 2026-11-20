using System.Threading.Channels;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Yakku.Application.PushNotifications.Batching;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Audit;
using Yakku.Application.Guests;
using Yakku.Application.Auth;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Sms;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Configuration;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.Email;
using Yakku.Application.Email.Interfaces;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.PushNotifications;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Application.Sharing.Interfaces;
using Yakku.Application.System.Interfaces;
using Yakku.Application.Users.Interfaces;
using Yakku.Application.Votes.Interfaces;
using Yakku.Infrastructure.Audit;
using Yakku.Infrastructure.Configuration;
using Yakku.Infrastructure.Email;
using Yakku.Infrastructure.Jobs;
using Yakku.Infrastructure.Firebase;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Push;
using Yakku.Infrastructure.Persistence.Repositories;
using StackExchange.Redis;
using Yakku.Infrastructure.Redis;
using Yakku.Infrastructure.Sms;
using Yakku.Application.Storage;
using Yakku.Infrastructure.Storage;
using Yakku.Infrastructure.System;

namespace Yakku.Infrastructure
{
    public static class DependencyInjection
    {
        /// <summary>
        /// OTP SMS delivery through MSG91, plus the OTP policy (its validity is the <see cref="OtpOptions.ExpiryMinutes"/> constant).
        /// Bad or missing settings stop the app from starting. The AuthKey is read from configuration only
        /// (Render's dashboard / .env via the flat-env bridge) and goes nowhere but the MSG91 request header.
        /// </summary>
        public static IServiceCollection AddSmsDelivery(this IServiceCollection services, IConfiguration configuration)
        {
            var msg91 = Msg91Options.FromConfiguration(configuration);
            msg91.ThrowIfInvalid();
            services.AddSingleton(msg91);
            services.AddSingleton(OtpPolicy.Default);

            if (msg91.Enabled)
            {
                // One typed client from IHttpClientFactory: pooled handlers, HTTPS only, a hard timeout, no retries.
                services.AddHttpClient<ISmsService, Msg91SmsService>(client =>
                {
                    client.BaseAddress = new Uri("https://control.msg91.com/");
                    client.Timeout = TimeSpan.FromSeconds(10);
                    client.MaxResponseContentBufferSize = 64 * 1024;
                });
            }
            else
            {
                services.AddSingleton<ISmsService, DisabledSmsService>();
            }

            return services;
        }

        /// <summary>Test helper: builds SMS delivery from a flat MSG91_* key map.</summary>
        public static IServiceCollection AddSmsDelivery(
            this IServiceCollection services,
            Func<string, string?> readSetting)
        {
            var msg91 = Msg91Options.FromEnvironment(readSetting);
            msg91.ThrowIfInvalid();
            services.AddSingleton(msg91);
            services.AddSingleton(OtpPolicy.Default);

            if (msg91.Enabled)
            {
                services.AddHttpClient<ISmsService, Msg91SmsService>(client =>
                {
                    client.BaseAddress = new Uri("https://control.msg91.com/");
                    client.Timeout = TimeSpan.FromSeconds(10);
                    client.MaxResponseContentBufferSize = 64 * 1024;
                });
            }
            else
            {
                services.AddSingleton<ISmsService, DisabledSmsService>();
            }

            return services;
        }

        public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
        {
            BindAppOptions(services, configuration);
            AddPaymentConfiguration(services, configuration);

            MediaUploadPolicyFactory.ValidateStorageConfiguration(configuration);
            services.AddSmsDelivery(configuration);

            services.AddHttpContextAccessor();
            services.AddScoped<IPollRepository, PollRepository>();
            services.AddScoped<IMediaUploadSessionRepository, MediaUploadSessionRepository>();
            services.AddSingleton<IPollMediaBucketResolver, PollMediaBucketResolver>();
            services.AddSingleton<IMediaUploadPolicy>(sp =>
                MediaUploadPolicyFactory.Create(sp.GetRequiredService<IOptions<MediaUploadOptions>>().Value));
            services.AddSingleton<IObjectStorage, NeonObjectStorage>();
            services.AddSingleton<IMediaInspector, TagLibMediaInspector>();
            services.AddSingleton<IAuditLogWriter, AuditLogWriter>();
            services.AddSingleton(GuestCleanupOptions.Default);
            services.AddSingleton(AuditRetentionOptions.Default);
            services.AddScoped<AuditRetentionService>();
            services.AddHostedService<GuestCleanupWorker>();
            services.AddHostedService<SessionCleanupWorker>();
            services.AddScoped<Yakku.Application.SavedCards.Interfaces.ISavedCardRepository, SavedCardRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<Yakku.Application.Auth.Interfaces.ILoginSnapshotReader, LoginSnapshotReader>();
            services.AddScoped<IAccountDeletionRepository, AccountDeletionRepository>();
            services.AddScoped<IVotableContentRepository, VotableContentRepository>();
            services.AddScoped<IGuestVoteClaimRepository, GuestVoteClaimRepository>();
            services.AddHostedService<AccountDeletionWorker>();
            services.AddScoped<IUserSessionRepository, UserSessionRepository>();
            services.AddScoped<Yakku.Application.Streaks.Interfaces.IStreakRepository, StreakRepository>();
            services.AddScoped<Yakku.Application.Users.Interfaces.IUserStatsRepository, UserStatsRepository>();
            services.AddSingleton(TimeProvider.System);
            services.AddScoped<IAdminRepository, AdminRepository>();
            services.AddScoped<IAdminSessionRepository, AdminSessionRepository>();
            services.AddScoped<IGuestRepository, GuestRepository>();
            services.AddScoped<IVoteRepository, VoteRepository>();
            services.AddScoped<ICheckRepository, CheckRepository>();
            services.AddScoped<Yakku.Application.Community.Interfaces.ICommunityWaitlistRepository, CommunityWaitlistRepository>();
            services.AddScoped<Yakku.Application.Community.Interfaces.ICommunityQueuedQuestionRepository, CommunityQueuedQuestionRepository>();
            services.AddScoped<Yakku.Application.Checks.Themes.ICheckThemeRepository, CheckThemeRepository>();
            services.AddScoped<Yakku.Application.Personality.Interfaces.IPersonalityRepository, PersonalityRepository>();
            services.AddScoped<Yakku.Application.AdminUsers.Interfaces.IAdminUserRepository, AdminUserRepository>();
            services.AddScoped<Yakku.Application.AdminPolls.Interfaces.IAdminPollRepository, AdminPollRepository>();
            services.AddScoped<Yakku.Application.Reasons.Interfaces.IReasonRepository, ReasonRepository>();
            services.AddScoped<Yakku.Application.AdminReasonReports.Interfaces.IAdminReasonReportRepository, AdminReasonReportRepository>();
            services.AddScoped<Yakku.Application.Plans.Interfaces.IPlanRepository, PlanRepository>();
            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IOtpChallengeStore, RedisOtpChallengeStore>();
            services.AddScoped<Yakku.Application.Users.Interfaces.IAnonymousNameRepository, AnonymousNameRepository>();
            services.AddScoped<Yakku.Application.Users.Interfaces.IUserSettingsRepository, UserSettingsRepository>();
            services.AddScoped<Yakku.Application.Users.Interfaces.IAnonymousNameReservations, RedisAnonymousNameReservations>();
            services.AddScoped<IAdminAuthStore, RedisAdminAuthStore>();
            services.AddScoped<ISystemHealthService, SystemHealthService>();
            services.AddSingleton<IFirebaseNotificationProvider, FirebaseNotificationProvider>();
            services.AddSingleton<IEmailSender>(sp =>
            {
                var resend = sp.GetRequiredService<IOptions<ResendOptions>>().Value;
                Require(resend.ApiKey, "Resend:ApiKey", "RESEND_API_KEY");
                if (string.IsNullOrWhiteSpace(resend.OtpTemplateId))
                {
                    throw new InvalidOperationException(
                        "Resend:OtpTemplateId is not set. Define it in appsettings.json under Resend:OtpTemplateId.");
                }
                var logger = sp.GetRequiredService<ILogger<ResendEmailSender>>();
                return ResendEmailSender.Create(resend.ApiKey, resend.OtpTemplateId, logger);
            });
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var redis = sp.GetRequiredService<IOptions<RedisOptions>>().Value;
                Require(redis.Url, "Redis:Url", "REDIS_URL");
                return RedisConnection.Connect(redis.Url);
            });
            services.AddSingleton<IRedisClient>(sp =>
                new RedisClient(sp.GetRequiredService<IConnectionMultiplexer>()));
            services.AddSingleton<IRedisScriptClient>(sp => sp.GetRequiredService<IRedisClient>());
            services.Configure<Yakku.Application.Configuration.ResponseCacheOptions>(
                configuration.GetSection(Yakku.Application.Configuration.ResponseCacheOptions.SectionName));
            services.AddSingleton<Yakku.Application.Caching.IResponseCache, Redis.RedisResponseCache>();

            var emailChannel = Channel.CreateBounded<EmailJob>(new BoundedChannelOptions(OtpEmailQueueOptions.Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
            services.AddSingleton(emailChannel);
            services.AddSingleton<IOtpEmailQueue, OtpEmailQueue>();
            services.AddHostedService<OtpEmailWorker>();

            var adminEmailChannel = Channel.CreateBounded<AdminEmailJob>(new BoundedChannelOptions(OtpEmailQueueOptions.Capacity)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            });
            services.AddSingleton(adminEmailChannel);
            services.AddSingleton<IAdminEmailQueue, AdminEmailQueue>();
            services.AddHostedService<AdminEmailWorker>();

            // Frequency-based vote notification batching. Settings come from the "VoteNotifications" section or
            // VoteNotifications__* environment variables (a .env file is loaded into the environment above, so it
            // counts too) and are validated when the host starts.
            services.AddSingleton<IValidateOptions<VoteNotificationBatchingOptions>, VoteNotificationBatchingOptionsValidator>();
            services.AddOptions<VoteNotificationBatchingOptions>()
                .Configure<IConfiguration>((options, config) =>
                {
                    config.GetSection(VoteNotificationBatchingOptions.SectionName).Bind(options);
                })
                .ValidateOnStart();
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<VoteNotificationBatchingOptions>>().Value);
            services.AddSingleton<IVoteBatchingStore, VoteBatchingRedisStore>();
            services.AddSingleton<VoteBatchingPolicy>();
            services.AddSingleton<VoteBatchingEngine>();
            services.AddSingleton<VoteNotificationMetrics>();

            services.AddSingleton<VoteNotificationSignal>();
            services.AddScoped<IVoteNotificationOutbox, VoteNotificationOutbox>();
            services.AddScoped<ICardLifecycleNotifications, CardLifecycleNotifications>();
            services.AddScoped<VotePushSender>();
            services.AddScoped<VoteEventClassifier>();
            services.AddScoped<CardModeEvaluator>();
            services.AddScoped<VoteBatchDispatcher>();
            services.AddScoped<VoteNotificationDispatcher>();
            services.AddHostedService<VoteNotificationWorker>();
            services.AddHostedService<CardCloseNotificationWorker>();
            services.AddHostedService<CardShareNudgeWorker>();
            services.AddHostedService<VoteNotificationRetentionWorker>();
            services.AddScoped<Yakku.Application.AskBack.Interfaces.IAskBackRepository, AskBackRepository>();
            services.AddScoped<Yakku.Application.AskBack.Interfaces.IUserBlockLookup, UserBlockRepository>();
            services.AddScoped<Yakku.Application.Entitlements.IEntitlementRepository, EntitlementRepository>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentOrderRepository, PaymentOrderRepository>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentFulfillmentRepository, PaymentFulfillmentRepository>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentQueryRepository, PaymentQueryRepository>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentReconciliationRepository, PaymentReconciliationRepository>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IPaymentRefundRepository, PaymentRefundRepository>();
            services.AddScoped<Yakku.Application.Payments.Interfaces.IRazorpayWebhookInbox, RazorpayWebhookInboxRepository>();
            services.AddSingleton<Yakku.Application.Payments.IPaymentLogWriter, Payments.PaymentLogWriter>();
            services.AddHostedService<Payments.RazorpayWebhookWorker>();
            services.AddHostedService<Payments.PaymentReconciliationWorker>();
            services.AddScoped<AskBackPushDispatcher>();
            services.AddHostedService<AskBackPushWorker>();

            return services;
        }

        private static void AddPaymentConfiguration(IServiceCollection services, IConfiguration configuration)
        {
            var razorpay = RazorpayOptions.FromConfiguration(configuration);
            razorpay.ThrowIfInvalid();
            services.AddSingleton(razorpay);

            services.Configure<PaymentOptions>(configuration.GetSection(PaymentOptions.SectionName));
            services.AddSingleton(sp =>
            {
                var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<PaymentOptions>>().Value;
                if (!string.Equals(options.Provider, "Razorpay", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        $"Payment:Provider '{options.Provider}' is not supported. Only Razorpay is supported.");
                }

                if (string.IsNullOrWhiteSpace(options.Currency) || options.Currency.Trim().Length != 3)
                {
                    throw new InvalidOperationException(
                        "Payment:Currency must be a 3-letter ISO 4217 code (for example INR).");
                }

                if (options.OrderExpiryMinutes is < 1 or > 24 * 60)
                {
                    throw new InvalidOperationException(
                        "Payment:OrderExpiryMinutes must be between 1 and 1440.");
                }

                if (!Enum.TryParse<Yakku.Domain.Enums.PaymentCaptureMode>(options.CaptureMode, ignoreCase: true, out _))
                {
                    throw new InvalidOperationException(
                        "Payment:CaptureMode must be Automatic or Manual.");
                }

                return options;
            });

            if (razorpay.Enabled)
            {
                services.AddHttpClient<Yakku.Application.Payments.Interfaces.IRazorpayClient, Payments.RazorpayClient>(
                    (sp, client) =>
                    {
                        var options = sp.GetRequiredService<RazorpayOptions>();
                        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
                        client.Timeout = TimeSpan.FromSeconds(15);
                        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                        client.MaxResponseContentBufferSize = 256 * 1024;
                    });
            }
            else
            {
                services.AddSingleton<Yakku.Application.Payments.Interfaces.IRazorpayClient, Payments.DisabledRazorpayClient>();
            }
        }

        private static void BindAppOptions(IServiceCollection services, IConfiguration configuration)
        {
            services.Configure<DatabaseOptions>(configuration.GetSection(DatabaseOptions.SectionName));
            services.Configure<RedisOptions>(configuration.GetSection(RedisOptions.SectionName));
            services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
            services.Configure<CorsSettings>(configuration.GetSection(CorsSettings.SectionName));
            services.Configure<ResendOptions>(configuration.GetSection(ResendOptions.SectionName));
            services.Configure<FirebaseOptions>(configuration.GetSection(FirebaseOptions.SectionName));
            services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.SectionName));
            services.Configure<MediaUploadOptions>(configuration.GetSection(MediaUploadOptions.SectionName));
            services.Configure<WebOptions>(configuration.GetSection(WebOptions.SectionName));
            services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
            services.Configure<ForwardedHeadersAppOptions>(configuration.GetSection(ForwardedHeadersAppOptions.SectionName));
        }

        private static void Require(string? value, string configKey, string envName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{configKey} is not set. Add {envName} to your .env file or environment variables.");
            }
        }
    }
}
