using Microsoft.EntityFrameworkCore;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Redis;

namespace Yakku.Infrastructure.System
{
    public class SystemHealthService : ISystemHealthService
    {
        private readonly YakkuDbContext _dbContext;
        private readonly UpstashRedisClient _redis;

        public SystemHealthService(YakkuDbContext dbContext, UpstashRedisClient redis)
        {
            _dbContext = dbContext;
            _redis = redis;
        }

        public async Task<SystemHealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
        {
            var checks = new List<SystemHealthCheck>
            {
                new()
                {
                    Name = "api",
                    Status = "Healthy"
                },
                await CheckDatabaseAsync(cancellationToken),
                await CheckRedisAsync(cancellationToken)
            };

            var healthy = checks.TrueForAll(check => check.Status == "Healthy");

            return new SystemHealthResponse
            {
                Status = healthy ? "Healthy" : "Unhealthy",
                CheckedAt = DateTime.UtcNow,
                Checks = checks
            };
        }

        private async Task<SystemHealthCheck> CheckDatabaseAsync(CancellationToken cancellationToken)
        {
            try
            {
                var connected = await _dbContext.Database.CanConnectAsync(cancellationToken);
                return new SystemHealthCheck
                {
                    Name = "database",
                    Status = connected ? "Healthy" : "Unhealthy",
                    Error = connected ? null : "Unavailable"
                };
            }
            catch
            {
                return new SystemHealthCheck
                {
                    Name = "database",
                    Status = "Unhealthy",
                    Error = "Unavailable"
                };
            }
        }

        private async Task<SystemHealthCheck> CheckRedisAsync(CancellationToken cancellationToken)
        {
            try
            {
                var reachable = await _redis.PingAsync(cancellationToken);
                return new SystemHealthCheck
                {
                    Name = "redis",
                    Status = reachable ? "Healthy" : "Unhealthy",
                    Error = reachable ? null : "Unavailable"
                };
            }
            catch
            {
                return new SystemHealthCheck
                {
                    Name = "redis",
                    Status = "Unhealthy",
                    Error = "Unavailable"
                };
            }
        }
    }
}
