namespace Yakku.Application.Polls.DTOs
{
    public class CreateMediaUploadSessionRequest
    {
        public string MediaType { get; set; } = string.Empty;
        public string? FileName { get; set; }
        public string ContentType { get; set; } = string.Empty;
        public long? FileSize { get; set; }
        public long SelectionVersion { get; set; }
        public Guid? PollId { get; set; }
    }

    public class CompleteMediaUploadRequest
    {
        public long SelectionVersion { get; set; }
    }

    public class MediaUploadTargetResponse
    {
        public string Method { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public Dictionary<string, string> Headers { get; set; } = [];
    }

    public class MediaUploadSessionResponse
    {
        public Guid SessionId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public string MimeType { get; set; } = string.Empty;
        public string ObjectKey { get; set; } = string.Empty;
        public long? ExpectedFileSize { get; set; }
        public long? ActualFileSize { get; set; }
        public long? DurationMs { get; set; }
        public long SelectionVersion { get; set; }
        public MediaUploadTargetResponse? Upload { get; set; }
        public DateTime ExpiresAt { get; set; }
        public string? FailureReason { get; set; }
    }

    public class MediaAccessResponse
    {
        public Guid SessionId { get; set; }
        public string Url { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
