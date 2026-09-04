using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Yakku.Infrastructure.Persistence
{
    public class YakkuDbContextFactory : IDesignTimeDbContextFactory<YakkuDbContext>
    {
        public YakkuDbContext CreateDbContext(string[] args)
        {
            LoadEnvFile();

            var connectionString = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                throw new InvalidOperationException(
                    "DB_CONNECTION_STRING is not set. Add it to your .env file or environment variables.");
            }

            var options = new DbContextOptionsBuilder<YakkuDbContext>()
                .UseNpgsql(PostgresConnection.Normalize(connectionString))
                .Options;

            return new YakkuDbContext(options);
        }

        private static void LoadEnvFile()
        {
            foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                var directory = new DirectoryInfo(Path.GetFullPath(start));
                while (directory is not null)
                {
                    var envPath = Path.Combine(directory.FullName, ".env");
                    if (!File.Exists(envPath))
                    {
                        envPath = Path.Combine(directory.FullName, "Backend", ".env");
                    }

                    if (File.Exists(envPath))
                    {
                        foreach (var line in File.ReadAllLines(envPath))
                        {
                            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                            {
                                continue;
                            }

                            var separator = line.IndexOf('=');
                            if (separator <= 0)
                            {
                                continue;
                            }

                            var key = line[..separator].Trim();
                            var value = line[(separator + 1)..].Trim();
                            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
                            {
                                Environment.SetEnvironmentVariable(key, value);
                            }
                        }

                        return;
                    }

                    directory = directory.Parent;
                }
            }
        }
    }
}
