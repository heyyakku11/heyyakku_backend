namespace Yakku.Domain.Enums
{
    public enum NotificationEventType
    {
        PollAnswered = 0,
        PollExpiring = 1,
        PollClosed = 2,
        CommentAdded = 3,
        CommentReplyAdded = 4,
        AccountSecurity = 5,
        SpecialOffer = 6,
        SystemUpdate = 7,
        ContentFlagged = 8
    }
}
