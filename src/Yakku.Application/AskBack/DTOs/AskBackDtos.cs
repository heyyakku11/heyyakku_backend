namespace Yakku.Application.AskBack.DTOs
{
    public class CreateAskBackConversationRequest
    {
        /// <summary>The vote (the reason's id) to ask back about.</summary>
        public Guid VoteId { get; set; }
    }

    public class SendAskBackMessageRequest
    {
        /// <summary>Chosen by the client for each message; sending it again returns the same message.</summary>
        public Guid ClientMessageId { get; set; }
        public string Content { get; set; } = string.Empty;
    }

    public class MarkAskBackReadRequest
    {
        public Guid LastReadMessageId { get; set; }
    }

    public class BlockAskBackRequest
    {
        /// <summary>Optional. When given, it is also recorded as a report of the other participant.</summary>
        public string? Reason { get; set; }
    }

    /// <summary>A participant as the other one sees them: only the Yakku-generated anonymous name.</summary>
    public class AskBackParticipantResponse
    {
        public string AnonymousName { get; set; } = string.Empty;

        /// <summary>True when that account was permanently deleted.</summary>
        public bool IsDeleted { get; set; }
    }

    /// <summary>What the conversation is about. Read live from the vote; never copied into a message.</summary>
    public class AskBackContextResponse
    {
        /// <summary>poll or check.</summary>
        public string ContentType { get; set; } = string.Empty;
        public string? Question { get; set; }

        /// <summary>The option the voter picked, or their custom answer. Null when the vote no longer exists.</summary>
        public string? SelectedOption { get; set; }

        /// <summary>The voter's reason. Null when it was removed (reported) or the vote no longer exists.</summary>
        public string? Reason { get; set; }
    }

    public class AskBackMessagePreviewResponse
    {
        public string Preview { get; set; } = string.Empty;
        public bool IsMine { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AskBackConversationResponse
    {
        public Guid Id { get; set; }

        /// <summary>active, closed or blocked.</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>asker (the poll or check owner) or voter.</summary>
        public string MyRole { get; set; } = string.Empty;
        public AskBackParticipantResponse Other { get; set; } = new();
        public AskBackContextResponse Context { get; set; } = new();
        public DateTime CreatedAt { get; set; }
        public DateTime? ClosedAt { get; set; }
        public bool IsMuted { get; set; }
        public int UnreadCount { get; set; }

        /// <summary>The caller can send a message now.</summary>
        public bool CanSend { get; set; }

        /// <summary>The caller can reopen it (closed, and the caller is the asker).</summary>
        public bool CanReopen { get; set; }

        /// <summary>The caller can lift the block (blocked, and the caller blocked).</summary>
        public bool CanUnblock { get; set; }
    }

    public class AskBackConversationListItemResponse
    {
        public Guid Id { get; set; }
        public string Status { get; set; } = string.Empty;
        public string MyRole { get; set; } = string.Empty;
        public AskBackParticipantResponse Other { get; set; } = new();
        public AskBackMessagePreviewResponse? LastMessage { get; set; }
        public DateTime? LastMessageAt { get; set; }
        public DateTime LastActivityAt { get; set; }
        public int UnreadCount { get; set; }
        public bool IsMuted { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class AskBackMessageResponse
    {
        public Guid Id { get; set; }
        public Guid ConversationId { get; set; }
        public Guid ClientMessageId { get; set; }
        public string Content { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;

        /// <summary>True when the caller sent it. The sender's user id is never returned.</summary>
        public bool IsMine { get; set; }
        public DateTime CreatedAt { get; set; }

        /// <summary>Pass as <c>cursor</c> to page from this message.</summary>
        public string Cursor { get; set; } = string.Empty;
    }

    public class AskBackPage<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

        /// <summary>Pass as <c>cursor</c> for the next page; null when there is no more.</summary>
        public string? NextCursor { get; set; }
        public bool HasMore { get; set; }
    }

    public class AskBackReadResponse
    {
        public Guid LastReadMessageId { get; set; }

        /// <summary>False when the position was not newer than the current one and was ignored.</summary>
        public bool Advanced { get; set; }
        public int UnreadCount { get; set; }
    }
}
