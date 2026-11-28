using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Tests;

/// <summary>
/// The plan moved from the users table to user_plans (1:1 over user_id). The migration has to carry every user's
/// plan and dates across before the old columns go, and reverse cleanly.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class UserPlanMigrationTests
{
    private const string Before = "20261128062906_AddConversationTopUps";
    private const string After = "20261128085309_AddUserPlansTable";

    private static readonly Guid FreeId = new("a1f3c0de-0000-4000-8000-000000000001");
    private static readonly Guid LiteId = new("a1f3c0de-0000-4000-8000-000000000002");
    private static readonly Guid LoveId = new("a1f3c0de-0000-4000-8000-000000000003");

    private readonly PostgresFixture _fixture;

    public UserPlanMigrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task TheMigration_CopiesEveryUsersPlanAndDates_DropsTheOldColumns_ThenReverses()
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
        try
        {
            var options = new DbContextOptionsBuilder<YakkuDbContext>().UseNpgsql(builder.ConnectionString).Options;
            await using var context = new YakkuDbContext(options);
            var migrator = context.GetService<IMigrator>();
            await migrator.MigrateAsync(Before);

            var onFree = Guid.NewGuid();
            var onLite = Guid.NewGuid();
            var onLove = Guid.NewGuid();
            var liteActivated = new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc);
            var liteExpires = liteActivated.AddDays(7);
            var loveActivated = new DateTime(2026, 11, 25, 8, 30, 0, DateTimeKind.Utc);
            var loveExpires = loveActivated.AddDays(30);

            await InsertUserAsync(context, onFree, FreeId, null, null);
            await InsertUserAsync(context, onLite, LiteId, liteActivated, liteExpires);
            await InsertUserAsync(context, onLove, LoveId, loveActivated, loveExpires);

            await migrator.MigrateAsync(After);

            var free = await PlanRowAsync(context, onFree);
            Assert.Equal("free", free.PlanCode);
            Assert.Null(free.ActivatedAt);
            Assert.Null(free.ExpiresAt);

            var lite = await PlanRowAsync(context, onLite);
            Assert.Equal("yakku_lite", lite.PlanCode);
            Assert.Equal(liteActivated, lite.ActivatedAt);
            Assert.Equal(liteExpires, lite.ExpiresAt);

            var love = await PlanRowAsync(context, onLove);
            Assert.Equal("yakku_love", love.PlanCode);
            Assert.Equal(loveActivated, love.ActivatedAt);
            Assert.Equal(loveExpires, love.ExpiresAt);

            // One row per user, and the plan columns are gone from users.
            var users = await context.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM users""").SingleAsync();
            var rows = await context.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM user_plans""").SingleAsync();
            Assert.Equal(users, rows);
            var oldColumns = await context.Database
                .SqlQuery<string>($"""SELECT column_name AS "Value" FROM information_schema.columns WHERE table_name = 'users' AND column_name IN ('PlanId','PlanActivatedAt','PlanExpiresAt')""")
                .ToListAsync();
            Assert.Empty(oldColumns);

            // Going back restores the columns from user_plans.
            await migrator.MigrateAsync(Before);

            Assert.Equal(FreeId, await PlanIdAsync(context, onFree));
            Assert.Equal(LiteId, await PlanIdAsync(context, onLite));
            Assert.Equal(LoveId, await PlanIdAsync(context, onLove));
            var restored = await context.Database
                .SqlQuery<DateTime>($"""SELECT "PlanExpiresAt" AS "Value" FROM users WHERE "Id" = {onLite}""")
                .SingleAsync();
            Assert.Equal(liteExpires, restored);
            Assert.Null(await context.Database
                .SqlQuery<string>($"""SELECT to_regclass('user_plans')::text AS "Value" """)
                .SingleAsync());
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

    private static Task InsertUserAsync(
        YakkuDbContext context, Guid id, Guid planId, DateTime? activatedAt, DateTime? expiresAt)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO users ("Id","PhoneNumber","AnonymousName","Status","PlanId","PlanActivatedAt","PlanExpiresAt","CreatedAt","UpdatedAt")
            VALUES ({id}, {"+1555" + id.ToString("N")[..10]}, {"plan-" + id.ToString("N")[..12]}, 'Active', {planId}, {activatedAt}, {expiresAt}, now(), now())
            """);

    private static Task<PlanRow> PlanRowAsync(YakkuDbContext context, Guid userId)
        => context.Database
            .SqlQuery<PlanRow>($"""
                SELECT plan_code AS "PlanCode", plan_activated_at AS "ActivatedAt", plan_expires_at AS "ExpiresAt"
                FROM user_plans WHERE user_id = {userId}
                """)
            .SingleAsync();

    private static Task<Guid> PlanIdAsync(YakkuDbContext context, Guid userId)
        => context.Database
            .SqlQuery<Guid>($"""SELECT "PlanId" AS "Value" FROM users WHERE "Id" = {userId}""")
            .SingleAsync();

    private sealed class PlanRow
    {
        public string PlanCode { get; set; } = string.Empty;
        public DateTime? ActivatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }
    }
}
