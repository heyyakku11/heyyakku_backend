namespace Yakku.Application.Notifications.DTOs
{
    public class NotificationResponse
    {
        public Guid Id { get; set; }
        public string Type { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Body { get; set; } = string.Empty;
        public string? Data { get; set; }
        public bool IsRead { get; set; }
        public DateTime? ReadAt { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ExpiresAt { get; set; }

        /// <summary>Ask Back actor for inbox avatars; null for card/system rows.</summary>
        public NotificationActorResponse? Actor { get; set; }

        /// <summary>Where the client should navigate when the row is opened.</summary>
        public NotificationTargetResponse Target { get; set; } = new();
    }

    public class NotificationActorResponse
    {
        public Guid? UserId { get; set; }
        public string? AnonymousName { get; set; }
        public string? AvatarTone { get; set; }
    }

    public class NotificationTargetResponse
    {
        /// <summary>
        /// askBackConversation | cardResults | cardResultsReady | personalityReady | cardShare.
        /// <c>personalityReady</c> / <c>cardResultsReady</c> are celebration-only targets from the ready push.
        /// </summary>
        public string Screen { get; set; } = string.Empty;

        public Guid? ConversationId { get; set; }
        public string? ContentType { get; set; }
        public Guid? ContentId { get; set; }
        public string? DeepLink { get; set; }
    }
}
