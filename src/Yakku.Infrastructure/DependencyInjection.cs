using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Categories.Interfaces;
using Yakku.Application.Devices.Interfaces;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Images.Interfaces;
using Yakku.Application.NotificationPreferences.Interfaces;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.System.Interfaces;
using Yakku.Application.Votes.Interfaces;
using Yakku.Infrastructure.Cloudinary;
using Yakku.Infrastructure.Configuration;
using Yakku.Infrastructure.Email;
using Yakku.Infrastructure.Persistence.Repositories;
using Yakku.Infrastructure.Redis;
using Yakku.Infrastructure.System;

namespace Yakku.Infrastructure
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructure(this IServiceCollection services)
        {
            EnvFile.Load();

            services.AddScoped<IPollRepository, PollRepository>();
            services.AddScoped<IImageRepository, ImageRepository>();
            services.AddScoped<ICategoryRepository, CategoryRepository>();
            services.AddScoped<ICloudinaryImageUploader, CloudinaryImageUploader>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserSessionRepository, UserSessionRepository>();
            services.AddScoped<IGuestRepository, GuestRepository>();
            services.AddScoped<IVoteRepository, VoteRepository>();
            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
            services.AddScoped<INotificationRepository, NotificationRepository>();
            services.AddScoped<IOtpChallengeStore, RedisOtpChallengeStore>();
            services.AddScoped<ISystemHealthService, SystemHealthService>();
            services.AddSingleton<ISystemLogWriter, SystemLogWriter>();
            services.AddSingleton<IEmailSender>(sp =>
            {
                var logger = sp.GetRequiredService<ILogger<ResendEmailSender>>();
                return ResendEmailSender.Create(
                    EnvFile.GetRequired("RESEND_API_KEY"),
                    EnvFile.GetRequired("RESEND_FROM_EMAIL"),
                    EnvFile.GetRequired("RESEND_FROM_NAME"),
                    EnvFile.GetRequired("RESEND_OTP_TEMPLATE_ID"),
                    logger);
            });
            services.AddSingleton(_ =>
            {
                var url = EnvFile.GetRequired("UPSTASH_REDIS_REST_URL");
                var token = EnvFile.GetRequired("UPSTASH_REDIS_REST_TOKEN");
                return UpstashRedisClient.Create(url, token);
            });

            return services;
        }
    }
}
