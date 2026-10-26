using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Tests;

/// <summary>
/// Runs the MakePushAllowedPerDevice migration over real data in a scratch database: the old per-user switch must not
/// be copied blindly to every device.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class PushAllowedMigrationTests
{
    private const string Before = "20261026061515_AddUserStreaks";
    private const string After = "20261026101402_MakePushAllowedPerDevice";
    private readonly PostgresFixture _fixture;

    public PushAllowedMigrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task OnlyDevicesWithAGrantedPermissionOfAUserWhoAllowedPush_AreSwitchedOn()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var scratch = "migration_check_" + Guid.NewGuid().ToString("N");
        var builder = new NpgsqlConnectionStringBuilder(_fixture.ConnectionString);
        await using (var admin = new NpgsqlConnection(_fixture.ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{scratch}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        builder.Database = scratch;
        var connectionString = builder.ConnectionString;
        try
        {
            var options = new DbContextOptionsBuilder<YakkuDbContext>().UseNpgsql(connectionString).Options;
            await using var context = new YakkuDbContext(options);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(Before);

            var optedIn = Guid.NewGuid();
            var optedOut = Guid.NewGuid();
            foreach (var (id, allowed) in new[] { (optedIn, true), (optedOut, false) })
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO users ("Id","PhoneNumber","Status","PlanId","CreatedAt","UpdatedAt")
                    VALUES ({id}, {"+1555" + id.ToString("N")[..10]}, 'Active', 'a1f3c0de-0000-4000-8000-000000000001', now(), now())
                    """);
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO user_profiles ("Id","UserId","Name","AnonymousName","IsPushAllowed","CreatedAt","UpdatedAt")
                    VALUES ({Guid.NewGuid()}, {id}, 'N', {"anon-" + id.ToString("N")[..12]}, {allowed}, now(), now())
                    """);
            }

            var grantedOptedIn = await AddDeviceAsync(context, optedIn, "Granted");
            var unknownOptedIn = await AddDeviceAsync(context, optedIn, "Unknown");
            var deniedOptedIn = await AddDeviceAsync(context, optedIn, "Denied");
            var noPermissionOptedIn = await AddDeviceAsync(context, optedIn, null);
            var grantedOptedOut = await AddDeviceAsync(context, optedOut, "Granted");
            var grantedUnowned = await AddDeviceAsync(context, null, "Granted");

            await migrator.MigrateAsync(After);

            Assert.True(await IsAllowedAsync(context, grantedOptedIn));
            Assert.False(await IsAllowedAsync(context, unknownOptedIn));
            Assert.False(await IsAllowedAsync(context, deniedOptedIn));
            Assert.False(await IsAllowedAsync(context, noPermissionOptedIn));
            Assert.False(await IsAllowedAsync(context, grantedOptedOut)); // the user had opted out
            Assert.False(await IsAllowedAsync(context, grantedUnowned)); // nobody to have allowed it

            // The migration is reversible and keeps the user-level opt-out.
            await migrator.MigrateAsync(Before);
            var profileSwitch = await context.Database
                .SqlQuery<bool>($"""SELECT "IsPushAllowed" AS "Value" FROM user_profiles WHERE "UserId" = {optedOut}""")
                .SingleAsync();
            Assert.False(profileSwitch);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await using var admin = new NpgsqlConnection(_fixture.ConnectionString);
            await admin.OpenAsync();
            await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{scratch}\" WITH (FORCE)", admin);
            await drop.ExecuteNonQueryAsync();
        }
    }

    private static async Task<Guid> AddDeviceAsync(YakkuDbContext context, Guid? userId, string? permission)
    {
        var id = Guid.NewGuid();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO devices ("Id","UserId","InstallationId","Platform","NotificationPermission","IsActive","CreatedAt","UpdatedAt")
            VALUES ({id}, {userId}, {"inst-" + id.ToString("N")}, 'Android', {permission}, TRUE, now(), now())
            """);
        return id;
    }

    private static Task<bool> IsAllowedAsync(YakkuDbContext context, Guid deviceId)
    {
        return context.Database
            .SqlQuery<bool>($"""SELECT "IsPushAllowed" AS "Value" FROM devices WHERE "Id" = {deviceId}""")
            .SingleAsync();
    }
}
