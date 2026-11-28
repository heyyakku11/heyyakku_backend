using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Yakku.Application.Caching;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;
using Yakku.Application.Users.Services;
using Yakku.Application.Users.Validators;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

public sealed class UserPermissionUnitTests
{
    private static readonly DateTime Now = new(2026, 11, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ANewUser_StartsWithTheDefaultPermissions()
    {
        var user = new User("+919000000001", "QuietFox");

        Assert.NotNull(user.Permissions);
        Assert.Equal(user.Id, user.Permissions.UserId);
        Assert.True(user.Permissions.AllowAskBack);
        Assert.False(user.Permissions.PushNotification);
        Assert.True(user.Permissions.Sms);
        Assert.False(user.Permissions.WhatsApp);
        Assert.Equal("QuietFox", user.AnonymousName);
    }

    [Fact]
    public void Apply_ChangesOnlyThePermissionsThatAreGiven()
    {
        var permission = new User("+919000000002", "BraveOwl").Permissions;

        permission.Apply(allowAskBack: false, pushNotification: null, sms: null, whatsApp: null, Now);

        Assert.False(permission.AllowAskBack);
        Assert.False(permission.PushNotification);
        Assert.True(permission.Sms);
        Assert.False(permission.WhatsApp);
        Assert.Equal(Now, permission.UpdatedAt);

        permission.Apply(allowAskBack: null, pushNotification: true, sms: false, whatsApp: false, Now.AddMinutes(1));

        Assert.False(permission.AllowAskBack);
        Assert.True(permission.PushNotification);
        Assert.False(permission.Sms);
        Assert.False(permission.WhatsApp);
    }

    [Fact]
    public void TheValidator_NeedsAtLeastOnePermission()
    {
        var result = new UpdateUserPermissionsValidator().Validate(new UpdateUserPermissionsRequest());

        Assert.False(result.IsValid);
        Assert.Equal("permissions", Assert.Single(result.Errors).PropertyName);
    }

    [Theory]
    [InlineData(nameof(UpdateUserPermissionsRequest.AllowAskBack))]
    [InlineData(nameof(UpdateUserPermissionsRequest.PushNotification))]
    [InlineData(nameof(UpdateUserPermissionsRequest.Sms))]
    [InlineData(nameof(UpdateUserPermissionsRequest.WhatsApp))]
    public void TheValidator_AcceptsAnySinglePermission(string property)
    {
        var request = new UpdateUserPermissionsRequest();
        typeof(UpdateUserPermissionsRequest).GetProperty(property)!.SetValue(request, false);

        Assert.True(new UpdateUserPermissionsValidator().Validate(request).IsValid);
    }

    [Fact]
    public async Task Get_ForAUserWithoutPermissions_Is404()
    {
        var service = NewService(new FakeRepository(), out _);

        var exception = await Assert.ThrowsAsync<AppException>(() => service.GetAsync(Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task Update_WithNothingToChange_IsRejected_BeforeTheRepositoryIsTouched()
    {
        var repository = new FakeRepository { Current = new UserPermissionsResponse() };
        var service = NewService(repository, out var cache);

        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateAsync(Guid.NewGuid(), new UpdateUserPermissionsRequest()));

        Assert.Equal(0, repository.UpdateCalls);
        Assert.Empty(cache.Removed);
    }

    [Fact]
    public async Task Update_ForAUserWithoutPermissions_Is404()
    {
        var service = NewService(new FakeRepository(), out var cache);

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.UpdateAsync(Guid.NewGuid(), new UpdateUserPermissionsRequest { Sms = false }));

        Assert.Equal(404, exception.StatusCode);
        Assert.Empty(cache.Removed);
    }

    [Fact]
    public async Task Update_ReturnsEveryPermission_AndDropsTheCachedUserCopy()
    {
        var userId = Guid.NewGuid();
        var service = NewService(new FakeRepository { Current = new UserPermissionsResponse() }, out var cache);

        var result = await service.UpdateAsync(
            userId,
            new UpdateUserPermissionsRequest { PushNotification = true, WhatsApp = false });

        Assert.True(result.AllowAskBack);
        Assert.True(result.PushNotification);
        Assert.True(result.Sms);
        Assert.False(result.WhatsApp);
        Assert.Contains(ResponseCacheKeys.UsersMe(userId), cache.Removed);
    }

    private static UserPermissionService NewService(FakeRepository repository, out RecordingCache cache)
    {
        cache = new RecordingCache();
        return new UserPermissionService(
            repository,
            new UpdateUserPermissionsValidator(),
            new FixedTime(Now),
            cache);
    }

    private sealed class FixedTime(DateTime utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(utcNow, TimeSpan.Zero);
    }

    private sealed class FakeRepository : IUserPermissionRepository
    {
        public UserPermissionsResponse? Current { get; set; }
        public int UpdateCalls { get; private set; }

        public Task<UserPermissionsResponse?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult(Current);

        public Task<UserPermissionsResponse?> UpdateAsync(
            Guid userId,
            UpdateUserPermissionsRequest request,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            UpdateCalls++;
            if (Current is null)
            {
                return Task.FromResult<UserPermissionsResponse?>(null);
            }

            Current.AllowAskBack = request.AllowAskBack ?? Current.AllowAskBack;
            Current.PushNotification = request.PushNotification ?? Current.PushNotification;
            Current.Sms = request.Sms ?? Current.Sms;
            Current.WhatsApp = request.WhatsApp ?? Current.WhatsApp;
            return Task.FromResult<UserPermissionsResponse?>(Current);
        }
    }

    private sealed class RecordingCache : IResponseCache
    {
        public List<string> Removed { get; } = [];

        public Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan ttl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default) => factory(cancellationToken);

        public Task<T> GetOrSetAsync<T>(
            string key,
            TimeSpan softTtl,
            TimeSpan hardTtl,
            Func<CancellationToken, Task<T>> factory,
            CancellationToken cancellationToken = default) => factory(cancellationToken);

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            Removed.Add(key);
            return Task.CompletedTask;
        }

        public Task RemoveManyAsync(IEnumerable<string> keys, CancellationToken cancellationToken = default)
        {
            Removed.AddRange(keys);
            return Task.CompletedTask;
        }
    }
}

[Collection(PostgresCollection.Name)]
public sealed class UserPermissionIntegrationTests
{
    private const string Before = "20261127084757_WebSessionFlag";
    private const string After = "20261127174246_UserPermissionsAndAnonymousNameOnUsers";
    private const string FreePlanId = "a1f3c0de-0000-4000-8000-000000000001";

    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public UserPermissionIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task EveryNewUser_GetsAPermissionsRow_WithTheDefaults()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = NewUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();

        await using var check = _fixture.CreateContext();
        var stored = await new UserPermissionRepository(check).GetAsync(user.Id);

        Assert.NotNull(stored);
        Assert.True(stored.AllowAskBack);
        Assert.False(stored.PushNotification);
        Assert.True(stored.Sms);
        Assert.False(stored.WhatsApp);
    }

    [SkippableFact]
    public async Task Update_ChangesOnlyTheGivenPermissions_ForOnlyThatUser()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var mine = NewUser();
        var theirs = NewUser();
        context.Users.AddRange(mine, theirs);
        await context.SaveChangesAsync();

        var updated = await new UserPermissionRepository(context).UpdateAsync(
            mine.Id,
            new UpdateUserPermissionsRequest { AllowAskBack = false, PushNotification = true },
            DateTime.UtcNow);

        Assert.NotNull(updated);
        Assert.False(updated.AllowAskBack);
        Assert.True(updated.PushNotification);
        Assert.True(updated.Sms);
        Assert.False(updated.WhatsApp);

        await using var check = _fixture.CreateContext();
        var repository = new UserPermissionRepository(check);
        var mineNow = await repository.GetAsync(mine.Id);
        var theirsNow = await repository.GetAsync(theirs.Id);
        Assert.False(mineNow!.AllowAskBack);
        Assert.True(mineNow.PushNotification);
        Assert.True(theirsNow!.AllowAskBack);
        Assert.False(theirsNow.PushNotification);
    }

    [SkippableFact]
    public async Task Update_ForAnUnknownUser_ReturnsNull()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var result = await new UserPermissionRepository(context).UpdateAsync(
            Guid.NewGuid(), new UpdateUserPermissionsRequest { Sms = false }, DateTime.UtcNow);

        Assert.Null(result);
    }

    [SkippableFact]
    public async Task AskBack_IsReadFromThePermissionsRow()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = NewUser();
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var askBack = new AskBackRepository(context);

        Assert.True(await askBack.AcceptsAskBacksAsync(user.Id));

        await new UserPermissionRepository(context).UpdateAsync(
            user.Id, new UpdateUserPermissionsRequest { AllowAskBack = false }, DateTime.UtcNow);

        Assert.False(await askBack.AcceptsAskBacksAsync(user.Id));
        Assert.Equal(user.AnonymousName, await askBack.GetAnonymousNameAsync(user.Id));
    }

    [SkippableFact]
    public async Task TheMigration_KeepsNames_AndAskBackChoices_AndCreatesARowForEveryUser_ThenReverses()
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

            var allowsAskBack = Guid.NewGuid();
            var blocksAskBack = Guid.NewGuid();
            var withoutProfile = Guid.NewGuid();
            foreach (var id in new[] { allowsAskBack, blocksAskBack, withoutProfile })
            {
                await context.Database.ExecuteSqlInterpolatedAsync($"""
                    INSERT INTO users ("Id","PhoneNumber","Status","PlanId","CreatedAt","UpdatedAt")
                    VALUES ({id}, {"+1555" + id.ToString("N")[..10]}, 'Active', {Guid.Parse(FreePlanId)}, now(), now())
                    """);
            }

            await InsertProfileAsync(context, allowsAskBack, "Alpha", true);
            await InsertProfileAsync(context, blocksAskBack, "Beta", false);

            await migrator.MigrateAsync(After);

            Assert.Equal("Alpha", await NameAsync(context, allowsAskBack));
            Assert.Equal("Beta", await NameAsync(context, blocksAskBack));
            Assert.StartsWith("user-", await NameAsync(context, withoutProfile));

            var first = await PermissionsAsync(context, allowsAskBack);
            Assert.True(first.AskBack);
            Assert.False(first.Push);
            Assert.True(first.Sms);
            Assert.True(first.WhatsApp);
            Assert.False((await PermissionsAsync(context, blocksAskBack)).AskBack);
            Assert.True((await PermissionsAsync(context, withoutProfile)).AskBack);

            var users = await context.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM users""").SingleAsync();
            var rows = await context.Database.SqlQuery<int>($"""SELECT count(*)::int AS "Value" FROM user_permissions""").SingleAsync();
            Assert.Equal(users, rows);
            Assert.Null(await context.Database
                .SqlQuery<string>($"""SELECT to_regclass('user_profiles')::text AS "Value" """)
                .SingleAsync());

            // The names stay unique without regard to case.
            await Assert.ThrowsAnyAsync<Exception>(() => context.Database.ExecuteSqlInterpolatedAsync(
                $"""UPDATE users SET "AnonymousName" = 'ALPHA' WHERE "Id" = {blocksAskBack}"""));

            // Going back restores the profiles from users and user_permissions.
            await migrator.MigrateAsync(Before);

            var restoredName = await context.Database
                .SqlQuery<string>($"""SELECT "AnonymousName" AS "Value" FROM user_profiles WHERE "UserId" = {blocksAskBack}""")
                .SingleAsync();
            var restoredAskBack = await context.Database
                .SqlQuery<bool>($"""SELECT "AllowAskBack" AS "Value" FROM user_profiles WHERE "UserId" = {blocksAskBack}""")
                .SingleAsync();
            Assert.Equal("Beta", restoredName);
            Assert.False(restoredAskBack);
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

    private static Task InsertProfileAsync(YakkuDbContext context, Guid userId, string name, bool allowAskBack)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO user_profiles ("Id","UserId","AnonymousName","AllowAskBack","CreatedAt","UpdatedAt")
            VALUES ({Guid.NewGuid()}, {userId}, {name}, {allowAskBack}, now(), now())
            """);

    private static Task<string> NameAsync(YakkuDbContext context, Guid userId)
        => context.Database
            .SqlQuery<string>($"""SELECT "AnonymousName" AS "Value" FROM users WHERE "Id" = {userId}""")
            .SingleAsync();

    private static Task<PermissionRow> PermissionsAsync(YakkuDbContext context, Guid userId)
        => context.Database
            .SqlQuery<PermissionRow>($"""
                SELECT allow_ask_back AS "AskBack", push_notification AS "Push", sms AS "Sms", whatsapp AS "WhatsApp"
                FROM user_permissions WHERE user_id = {userId}
                """)
            .SingleAsync();

    private static User NewUser()
    {
        var number = Interlocked.Increment(ref _sequence);
        var unique = $"{Environment.ProcessId}{number}{Random.Shared.Next(1000, 9999)}";
        return new User($"+9199{unique}", $"Perm{unique}");
    }

    private sealed class PermissionRow
    {
        public bool AskBack { get; set; }
        public bool Push { get; set; }
        public bool Sms { get; set; }
        public bool WhatsApp { get; set; }
    }
}
