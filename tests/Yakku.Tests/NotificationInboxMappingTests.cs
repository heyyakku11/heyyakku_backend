using Yakku.Application.Notifications.Mapper;
using Yakku.Application.PushNotifications;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class NotificationInboxMappingTests
{
    [Fact]
    public void AskBackRow_ExposesActorAndConversationTarget()
    {
        var actorId = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var conversationId = Guid.NewGuid();
        var notification = new Notification(
            Guid.NewGuid(),
            NotificationType.Normal,
            NotificationEventType.AskBackReply,
            CardActivityCopy.AskBackReplyTitle,
            "Hello there",
            data: System.Text.Json.JsonSerializer.Serialize(new
            {
                kind = "ask_back_reply",
                conversationId,
                messageId = Guid.NewGuid(),
                actorUserId = actorId,
                actorAnonymousName = "QuietFox",
                avatarTone = CardActivityCopy.AvatarToneFor(actorId),
                contentType = "poll",
                contentId = Guid.NewGuid(),
                deepLink = $"yakku://ask-back/{conversationId}"
            }));

        var response = notification.ToResponse();

        Assert.Equal("askbackreply", response.EventType);
        Assert.NotNull(response.Actor);
        Assert.Equal(actorId, response.Actor!.UserId);
        Assert.Equal("QuietFox", response.Actor.AnonymousName);
        Assert.False(string.IsNullOrWhiteSpace(response.Actor.AvatarTone));
        Assert.Equal("askBackConversation", response.Target.Screen);
        Assert.Equal(conversationId, response.Target.ConversationId);
        Assert.Equal($"yakku://ask-back/{conversationId}", response.Target.DeepLink);
    }

    [Fact]
    public void NoVotesYetRow_TargetsCardShare()
    {
        var contentId = Guid.NewGuid();
        var notification = new Notification(
            Guid.NewGuid(),
            NotificationType.Normal,
            NotificationEventType.NoVotesYet,
            CardActivityCopy.NoVotesYetTitle,
            "\"Q?\" is waiting. Share it again?",
            data: System.Text.Json.JsonSerializer.Serialize(new
            {
                contentId,
                contentType = "poll",
                shareCode = "abc123def0"
            }));

        var response = notification.ToResponse();

        Assert.Equal("novotesyet", response.EventType);
        Assert.Null(response.Actor);
        Assert.Equal("cardShare", response.Target.Screen);
        Assert.Equal("poll", response.Target.ContentType);
        Assert.Equal(contentId, response.Target.ContentId);
    }
}
