using FluentValidation;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Users.DTOs;
using Yakku.Application.Users.Interfaces;
using Yakku.Application.Users.Services;
using Yakku.Application.Users.Validators;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>Unit tests: no database.</summary>
public sealed class UserSettingsUnitTests
{
    [Fact]
    public async Task AnEmptyUpdate_IsRejected()
    {
        var service = NewService(new FakeSettings());

        await Assert.ThrowsAsync<ValidationException>(
            () => service.UpdateAsync(Guid.NewGuid(), new UpdateUserSettingsRequest()));
    }

    [Fact]
    public async Task Update_ChangesOnlyTheSettingThatWasSent_AndReturnsAllOfThem()
    {
        var fake = new FakeSettings { AllowAskBack = true };
        var service = NewService(fake);

        var response = await service.UpdateAsync(Guid.NewGuid(), new UpdateUserSettingsRequest { AllowAskBack = false });

        Assert.False(response.AllowAskBack);
        Assert.False(fake.AllowAskBack);
    }

    [Fact]
    public async Task Update_ForAUserWithoutAProfile_Returns404()
    {
        var service = NewService(new FakeSettings { HasProfile = false });

        var exception = await Assert.ThrowsAsync<AppException>(
            () => service.UpdateAsync(Guid.NewGuid(), new UpdateUserSettingsRequest { AllowAskBack = true }));

        Assert.Equal(404, exception.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsTheCurrentSetting()
    {
        var service = NewService(new FakeSettings { AllowAskBack = false });

        Assert.False((await service.GetAsync(Guid.NewGuid())).AllowAskBack);
    }

    private static UserSettingsService NewService(FakeSettings fake)
        => new(fake, new UpdateUserSettingsValidator());

    private sealed class FakeSettings : IUserSettingsRepository
    {
        public bool AllowAskBack { get; set; } = true;
        public bool HasProfile { get; set; } = true;

        public Task<UserSettingsResponse?> GetAsync(Guid userId, CancellationToken cancellationToken = default)
            => Task.FromResult<UserSettingsResponse?>(
                HasProfile ? new UserSettingsResponse { AllowAskBack = AllowAskBack } : null);

        public Task<bool> SetAllowAskBackAsync(Guid userId, bool allow, CancellationToken cancellationToken = default)
        {
            if (!HasProfile) return Task.FromResult(false);
            AllowAskBack = allow;
            return Task.FromResult(true);
        }
    }
}

/// <summary>Integration tests against a real Postgres (Docker).</summary>
[Collection(PostgresCollection.Name)]
public sealed class UserSettingsIntegrationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public UserSettingsIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task ANewUser_AllowsAskBacksByDefault()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var user = await AddUserAsync();

        await using var context = _fixture.CreateContext();
        var settings = await new UserSettingsRepository(context).GetAsync(user.Id);

        Assert.True(settings!.AllowAskBack);
    }

    [SkippableFact]
    public async Task TheSetting_IsSavedPerUser_AndLeavesOthersAlone()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var mine = await AddUserAsync();
        var theirs = await AddUserAsync();

        await using (var context = _fixture.CreateContext())
        {
            Assert.True(await new UserSettingsRepository(context).SetAllowAskBackAsync(mine.Id, false));
        }

        await using var check = _fixture.CreateContext();
        var repository = new UserSettingsRepository(check);
        Assert.False((await repository.GetAsync(mine.Id))!.AllowAskBack);
        Assert.True((await repository.GetAsync(theirs.Id))!.AllowAskBack);
        Assert.False(await new AskBackRepository(check).AcceptsAskBacksAsync(mine.Id));
        Assert.True(await new AskBackRepository(check).AcceptsAskBacksAsync(theirs.Id));
    }

    [SkippableFact]
    public async Task ASettingForAMissingProfile_IsNotApplied()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        Assert.False(await new UserSettingsRepository(context).SetAllowAskBackAsync(Guid.NewGuid(), false));
    }

    private async Task<User> AddUserAsync()
    {
        var n = Interlocked.Increment(ref _sequence);
        await using var context = _fixture.CreateContext();
        var user = new User($"+98{n:D12}", $"Settings{n}{Guid.NewGuid():N}"[..30]);
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }
}
