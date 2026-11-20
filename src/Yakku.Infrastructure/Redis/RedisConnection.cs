using StackExchange.Redis;

namespace Yakku.Infrastructure.Redis
{
    /// <summary>Parses <c>REDIS_URL</c> into StackExchange options for any Redis host (local, Upstash TCP, Redis Cloud, etc.).</summary>
    public static class RedisConnection
    {
        public static ConfigurationOptions Parse(string connectionUrl)
        {
            if (string.IsNullOrWhiteSpace(connectionUrl))
            {
                throw new ArgumentException("Redis connection URL is required.", nameof(connectionUrl));
            }

            var raw = connectionUrl.Trim().Trim('"').Trim('\'');

            // StackExchange native form: "host:6379,password=...,ssl=True"
            if (!raw.Contains("://", StringComparison.Ordinal))
            {
                var options = ConfigurationOptions.Parse(raw);
                options.AbortOnConnectFail = false;
                return options;
            }

            if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
                || (uri.Scheme is not "redis" and not "rediss"))
            {
                throw new InvalidOperationException(
                    "Redis:Url / REDIS_URL must be a redis:// or rediss:// URL, or a StackExchange connection string.");
            }

            var ssl = uri.Scheme.Equals("rediss", StringComparison.OrdinalIgnoreCase);
            var port = uri.IsDefaultPort ? (ssl ? 6380 : 6379) : uri.Port;
            var optionsFromUri = new ConfigurationOptions
            {
                AbortOnConnectFail = false,
                Ssl = ssl
            };
            optionsFromUri.EndPoints.Add(uri.Host, port);

            if (!string.IsNullOrEmpty(uri.UserInfo))
            {
                var parts = uri.UserInfo.Split(':', 2);
                if (parts.Length == 2)
                {
                    var user = Uri.UnescapeDataString(parts[0]);
                    var password = Uri.UnescapeDataString(parts[1]);
                    if (!string.IsNullOrEmpty(user) && !string.Equals(user, "default", StringComparison.OrdinalIgnoreCase))
                    {
                        optionsFromUri.User = user;
                    }

                    optionsFromUri.Password = password;
                }
                else
                {
                    optionsFromUri.Password = Uri.UnescapeDataString(parts[0]);
                }
            }

            return optionsFromUri;
        }

        public static ConnectionMultiplexer Connect(string connectionUrl)
        {
            return ConnectionMultiplexer.Connect(Parse(connectionUrl));
        }
    }
}
