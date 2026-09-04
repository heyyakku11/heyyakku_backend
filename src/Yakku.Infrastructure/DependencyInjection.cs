using Microsoft.Extensions.DependencyInjection;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.System.Interfaces;
using Yakku.Application.Votes.Interfaces;
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
            services.AddScoped<IPollRepository, PollRepository>();
            services.AddScoped<IUserRepository, UserRepository>();
            services.AddScoped<IUserSessionRepository, UserSessionRepository>();
            services.AddScoped<IGuestRepository, GuestRepository>();
            services.AddScoped<IVoteRepository, VoteRepository>();
            services.AddScoped<IOtpChallengeStore, RedisOtpChallengeStore>();
            services.AddScoped<ISystemHealthService, SystemHealthService>();
            services.AddSingleton<ISystemLogWriter, SystemLogWriter>();
            services.AddSingleton<IEmailSender, LoggingEmailSender>();
            services.AddSingleton(_ =>
            {
                var url = GetRequired("UPSTASH_REDIS_REST_URL");
                var token = GetRequired("UPSTASH_REDIS_REST_TOKEN");
                return UpstashRedisClient.Create(url, token);
            });

            return services;
        }

        private static string GetRequired(string key)
        {
            var value = Environment.GetEnvironmentVariable(key)?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{key} is not set. Add it to your .env file or environment variables.");
            }

            return value;
        }
    }
}
