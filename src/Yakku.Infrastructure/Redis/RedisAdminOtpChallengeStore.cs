using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Auth.Models;

namespace Yakku.Infrastructure.Redis
{
    public sealed class RedisAdminOtpChallengeStore : RedisOtpChallengeStore, IAdminOtpChallengeStore
    {
        public RedisAdminOtpChallengeStore(UpstashRedisClient redis)
            : base(redis, "auth:admin-otp")
        {
        }
    }
}
