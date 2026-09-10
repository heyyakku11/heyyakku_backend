using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Yakku.Infrastructure.Configuration;

namespace Yakku.Infrastructure.Persistence
{
    public class YakkuDbContextFactory : IDesignTimeDbContextFactory<YakkuDbContext>
    {
        public YakkuDbContext CreateDbContext(string[] args)
        {
            EnvFile.Load();

            var connectionString = EnvFile.GetRequired("DB_CONNECTION_STRING");

            var options = new DbContextOptionsBuilder<YakkuDbContext>()
                .UseNpgsql(PostgresConnection.Normalize(connectionString))
                .Options;

            return new YakkuDbContext(options);
        }
    }
}
