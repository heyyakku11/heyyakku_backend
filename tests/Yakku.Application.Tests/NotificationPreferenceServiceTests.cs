using FluentValidation;
using Yakku.Application.NotificationPreferences.DTOs;
using Yakku.Application.NotificationPreferences.Interfaces;
using Yakku.Application.NotificationPreferences.Services;
using Yakku.Application.NotificationPreferences.Validators;
using Yakku.Domain.Entities;
using Xunit;

namespace Yakku.Application.Tests.NotificationPreferences;

public class NotificationPreferenceServiceTests
{
    [Fact]
    public async Task Get_WhenMissing_CreatesDefaults()
    {
        var fixture = PreferenceFixture.Create();
        var userId = Guid.NewGuid();

        var result = await fixture.Service.GetAsync(userId);

        Assert.Single(fixture.Preferences.Items);
        Assert.Equal(userId, fixture.Preferences.Items[0].UserId);
        Assert.True(result.PushEnabled);
        Assert.True(result.PollActivityEnabled);
        Assert.True(result.OffersEnabled);
        Assert.True(result.AlertsEnabled);
        Assert.True(result.NormalEnabled);
        Assert.Null(typeof(NotificationPreferenceResponse).GetProperty("Id"));
        Assert.Null(typeof(NotificationPreferenceResponse).GetProperty("UserId"));
        Assert.Null(typeof(NotificationPreferenceResponse).GetProperty("CreatedAt"));
        Assert.Null(typeof(NotificationPreferenceResponse).GetProperty("UpdatedAt"));
    }

    [Fact]
    public async Task Get_WhenExists_ReturnsExistingValues()
    {
        var fixture = PreferenceFixture.Create();
        var userId = Guid.NewGuid();
        var existing = new NotificationPreference(userId);
        existing.Update(pushEnabled: false, pollActivityEnabled: null, offersEnabled: false, alertsEnabled: null, normalEnabled: null);
        fixture.Preferences.Items.Add(existing);

        var result = await fixture.Service.GetAsync(userId);

        Assert.Single(fixture.Preferences.Items);
        Assert.False(result.PushEnabled);
        Assert.True(result.PollActivityEnabled);
        Assert.False(result.OffersEnabled);
        Assert.True(result.AlertsEnabled);
        Assert.True(result.NormalEnabled);
    }

    [Fact]
    public async Task Update_SuppliedFieldsOnly_LeavesOthersUnchanged()
    {
        var fixture = PreferenceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.GetAsync(userId);

        var result = await fixture.Service.UpdateAsync(
            userId,
            new UpdateNotificationPreferenceRequest { OffersEnabled = false });

        Assert.True(result.PushEnabled);
        Assert.True(result.PollActivityEnabled);
        Assert.False(result.OffersEnabled);
        Assert.True(result.AlertsEnabled);
        Assert.True(result.NormalEnabled);
        Assert.False(fixture.Preferences.Items[0].OffersEnabled);
    }

    [Fact]
    public async Task Update_FalseIsApplied_NotConfusedWithNull()
    {
        var fixture = PreferenceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.GetAsync(userId);

        var result = await fixture.Service.UpdateAsync(
            userId,
            new UpdateNotificationPreferenceRequest
            {
                PushEnabled = false,
                NormalEnabled = false
            });

        Assert.False(result.PushEnabled);
        Assert.True(result.PollActivityEnabled);
        Assert.True(result.OffersEnabled);
        Assert.True(result.AlertsEnabled);
        Assert.False(result.NormalEnabled);
    }

    [Fact]
    public async Task Update_EmptyRequest_ThrowsValidationException()
    {
        var fixture = PreferenceFixture.Create();
        var userId = Guid.NewGuid();

        await Assert.ThrowsAsync<ValidationException>(() =>
            fixture.Service.UpdateAsync(userId, new UpdateNotificationPreferenceRequest()));
    }

    [Fact]
    public async Task Update_PushEnabledFalse_DoesNotChangeCategoryFlags()
    {
        var fixture = PreferenceFixture.Create();
        var userId = Guid.NewGuid();
        await fixture.Service.GetAsync(userId);

        var result = await fixture.Service.UpdateAsync(
            userId,
            new UpdateNotificationPreferenceRequest { PushEnabled = false });

        Assert.False(result.PushEnabled);
        Assert.True(result.PollActivityEnabled);
        Assert.True(result.OffersEnabled);
        Assert.True(result.AlertsEnabled);
        Assert.True(result.NormalEnabled);
    }

    [Fact]
    public async Task Update_WhenMissing_CreatesDefaultsThenAppliesPatch()
    {
        var fixture = PreferenceFixture.Create();
        var userId = Guid.NewGuid();

        var result = await fixture.Service.UpdateAsync(
            userId,
            new UpdateNotificationPreferenceRequest { AlertsEnabled = false });

        Assert.Single(fixture.Preferences.Items);
        Assert.True(result.PushEnabled);
        Assert.True(result.PollActivityEnabled);
        Assert.True(result.OffersEnabled);
        Assert.False(result.AlertsEnabled);
        Assert.True(result.NormalEnabled);
    }

    private sealed class PreferenceFixture
    {
        public required NotificationPreferenceService Service { get; init; }
        public required FakeNotificationPreferenceRepository Preferences { get; init; }

        public static PreferenceFixture Create()
        {
            var preferences = new FakeNotificationPreferenceRepository();

            return new PreferenceFixture
            {
                Service = new NotificationPreferenceService(
                    preferences,
                    new UpdateNotificationPreferenceValidator()),
                Preferences = preferences
            };
        }
    }

    private sealed class FakeNotificationPreferenceRepository : INotificationPreferenceRepository
    {
        public List<NotificationPreference> Items { get; } = [];

        public Task<NotificationPreference?> GetByUserIdAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(preference => preference.UserId == userId));
        }

        public Task AddAsync(NotificationPreference preference, CancellationToken cancellationToken = default)
        {
            Items.Add(preference);
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
