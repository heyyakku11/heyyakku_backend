using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Notifications.DTOs;
using Yakku.Application.Notifications.Interfaces;
using Yakku.Application.Notifications.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Xunit;

namespace Yakku.Application.Tests.Notifications;

public class NotificationServiceTests
{
    [Fact]
    public async Task GetMine_ReturnsOnlyCurrentUsersNonExpired_NewestFirst()
    {
        var fixture = NotificationFixture.Create();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(1);

        var older = CreateNotification(userId, "Older", expiresAt);
        await Task.Delay(5);
        var newer = CreateNotification(userId, "Newer", expiresAt);
        var expired = CreateNotification(userId, "Expired", DateTime.UtcNow.AddMinutes(-1));
        var otherUsers = CreateNotification(otherUserId, "Other", expiresAt);

        fixture.Notifications.Items.AddRange([older, newer, expired, otherUsers]);

        var result = await fixture.Service.GetMineAsync(userId, cursor: null);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal(newer.Id, result.Items[0].Id);
        Assert.Equal(older.Id, result.Items[1].Id);
        Assert.Equal("Newer", result.Items[0].Title);
        Assert.False(result.Meta.HasMore);
        Assert.Null(result.Meta.NextCursor);
        Assert.Equal(10, result.Meta.PageSize);
        Assert.Null(typeof(NotificationResponse).GetProperty("UserId"));
    }

    [Fact]
    public async Task GetMine_WithMoreThanPageSize_ReturnsCursorAndNextPage()
    {
        var fixture = NotificationFixture.Create();
        var userId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(1);

        for (var i = 0; i < 11; i++)
        {
            fixture.Notifications.Items.Add(CreateNotification(userId, $"N{i}", expiresAt));
            await Task.Delay(2);
        }

        var firstPage = await fixture.Service.GetMineAsync(userId, cursor: null);

        Assert.Equal(10, firstPage.Items.Count);
        Assert.True(firstPage.Meta.HasMore);
        Assert.False(string.IsNullOrWhiteSpace(firstPage.Meta.NextCursor));

        var secondPage = await fixture.Service.GetMineAsync(userId, firstPage.Meta.NextCursor);

        Assert.Single(secondPage.Items);
        Assert.False(secondPage.Meta.HasMore);
        Assert.Null(secondPage.Meta.NextCursor);
        Assert.DoesNotContain(secondPage.Items[0].Id, firstPage.Items.Select(item => item.Id));
    }

    [Fact]
    public async Task MarkAsRead_SetsIsReadAndReadAt()
    {
        var fixture = NotificationFixture.Create();
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId, "Hello", DateTime.UtcNow.AddDays(1));
        fixture.Notifications.Items.Add(notification);

        var result = await fixture.Service.MarkAsReadAsync(userId, notification.Id);

        Assert.True(result.IsRead);
        Assert.NotNull(result.ReadAt);
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAt);
        Assert.Equal(1, fixture.Notifications.SaveChangesCount);
    }

    [Fact]
    public async Task MarkAsRead_WhenAlreadyRead_IsIdempotent()
    {
        var fixture = NotificationFixture.Create();
        var userId = Guid.NewGuid();
        var notification = CreateNotification(userId, "Hello", DateTime.UtcNow.AddDays(1));
        fixture.Notifications.Items.Add(notification);

        var first = await fixture.Service.MarkAsReadAsync(userId, notification.Id);
        var firstReadAt = first.ReadAt;

        await Task.Delay(5);
        var second = await fixture.Service.MarkAsReadAsync(userId, notification.Id);

        Assert.True(second.IsRead);
        Assert.Equal(firstReadAt, second.ReadAt);
        Assert.Equal(2, fixture.Notifications.SaveChangesCount);
    }

    [Fact]
    public async Task MarkAsRead_Missing_ReturnsNotFound()
    {
        var fixture = NotificationFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.MarkAsReadAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
    }

    [Fact]
    public async Task MarkAsRead_OtherUsersNotification_ReturnsNotFound()
    {
        var fixture = NotificationFixture.Create();
        var ownerId = Guid.NewGuid();
        var notification = CreateNotification(ownerId, "Secret", DateTime.UtcNow.AddDays(1));
        fixture.Notifications.Items.Add(notification);

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.MarkAsReadAsync(Guid.NewGuid(), notification.Id));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(ApiErrorCodes.NotFound, exception.ErrorCode);
        Assert.False(notification.IsRead);
    }

    private static Notification CreateNotification(Guid userId, string title, DateTime expiresAt)
    {
        return new Notification(
            userId,
            NotificationType.Normal,
            NotificationEventType.SystemUpdate,
            title,
            "Body",
            expiresAt);
    }

    private sealed class NotificationFixture
    {
        public required NotificationService Service { get; init; }
        public required FakeNotificationRepository Notifications { get; init; }

        public static NotificationFixture Create()
        {
            var notifications = new FakeNotificationRepository();
            return new NotificationFixture
            {
                Service = new NotificationService(notifications),
                Notifications = notifications
            };
        }
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<Notification> Items { get; } = [];
        public int SaveChangesCount { get; private set; }

        public Task<IReadOnlyList<Notification>> GetByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default)
        {
            var now = DateTime.UtcNow;
            var query = Items
                .Where(notification =>
                    notification.UserId == userId &&
                    notification.ExpiresAt > now);

            if (cursorCreatedAt is not null && cursorId is not null)
            {
                query = query.Where(notification =>
                    notification.CreatedAt < cursorCreatedAt.Value
                    || (notification.CreatedAt == cursorCreatedAt.Value && notification.Id.CompareTo(cursorId.Value) < 0));
            }

            IReadOnlyList<Notification> page = query
                .OrderByDescending(notification => notification.CreatedAt)
                .ThenByDescending(notification => notification.Id)
                .Take(take)
                .ToList();

            return Task.FromResult(page);
        }

        public Task<Notification?> GetByIdForUserAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                Items.FirstOrDefault(notification => notification.Id == id && notification.UserId == userId));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCount++;
            return Task.CompletedTask;
        }
    }
}
