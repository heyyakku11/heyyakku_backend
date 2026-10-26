using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Audit;
using Yakku.Application.Guests;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.Email;
using Yakku.Application.Email.Interfaces;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.Polls.Interfaces;
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
using Yakku.Infrastructure.Persistence.Repositories;
using Yakku.Infrastructure.Redis;
using Yakku.Application.Storage;
using Yakku.Infrastructure.Storage;
using Yakku.Infrastructure.System;

namespace Yakku.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            EnvFile.Load();
            MediaUploadPolicyFactory.ValidateStorageConfiguration();

            services.AddHttpContextAccessor();
            services.AddScoped<IPollRepository, PollRepository>();
            services.AddScoped<IMediaUploadSessionRepository, MediaUploadSessionRepository>();
            services.AddSingleton<IPollMediaBucketResolver, PollMediaBucketResolver>();
            services.AddSingleton<IMediaUploadPolicy>(_ => MediaUploadPolicyFactory.Create());
            services.AddSingleton<IObjectStorage, NeonObjectStorage>();
            services.AddSingleton<IMediaInspector, TagLibMediaInspector>();
            services.AddHostedService<MediaUploadCleanupWorker>();
            services.AddSingleton<IAuditLogWriter, AuditLogWriter>();
            services.AddSingleton(GuestCleanupOptions.Default);
            services.AddSingleton(AuditRetentionOptions.Default);
            services.AddScoped<AuditRetentionService>();
            services.AddHostedService<GuestCleanupWorker>();
            services.AddHostedService<AuditRetentionWorker>();
            services.AddScoped<ISavedPollRepository, SavedPollRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IAccountDeletionRepository, AccountDeletionRepository>();
            services.AddScoped<IVotableContentRepository, VotableContentRepository>();
            services.AddScoped<IGuestVoteClaimRepository, GuestVoteClaimRepository>();
            services.AddHostedService<AccountDeletionWorker>();
            services.AddScoped<IUserSessionRepository, UserSessionRepository>();
            services.AddScoped<IAdminRepository, AdminRepository>();
            services.AddScoped<IAdminSessionRepository, AdminSessionRepository>();
            services.AddScoped<IGuestRepository, GuestRepository>();
            services.AddScoped<IVoteRepository, VoteRepository>();
            services.AddScoped<ICheckRepository, CheckRepository>();            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IOtpChallengeStore, RedisOtpChallengeStore>();
            services.AddScoped<IAdminAuthStore, RedisAdminAuthStore>();
            services.AddScoped<ISystemHealthService, SystemHealthService>();
            services.AddSingleton<IFirebaseNotificationProvider, FirebaseNotificationProvider>();
            services.AddSingleton<IEmailSender>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ResendEmailSender>>();
                return ResendEmailSender.Create(
                    EnvFile.GetRequired("RESEND_API_KEY"),
                    EnvFile.GetRequired("RESEND_OTP_TEMPLATE_ID"),
                    logger);
            });
            services.AddSingleton(_ =>
            {
                var url = EnvFile.GetRequired("UPSTASH_REDIS_REST_URL");
                var token = EnvFile.GetRequired("UPSTASH_REDIS_REST_TOKEN");
                return UpstashRedisClient.Create(url, token);
            });

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

            return services;
        }
    }
}
