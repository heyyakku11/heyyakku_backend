namespace Yakku.Application.PushNotifications.DTOs
{
    public class FirebaseTokenSendResult
    {
        public required string TokenFingerprint { get; init; }
        public bool Success { get; init; }
        public string? MessageId { get; init; }
        public bool IsInvalidToken { get; init; }
        public string? ErrorCode { get; init; }
        public string? ErrorMessage { get; init; }
    }
}
