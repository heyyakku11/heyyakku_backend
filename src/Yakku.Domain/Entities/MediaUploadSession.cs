using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class MediaUploadSession
    {
        public const int FailureReasonMaxLength = 300;

        public Guid Id { get; private set; }
        public Guid? UserId { get; private set; }
        public Guid? GuestId { get; private set; }
        public Guid? PollId { get; private set; }
        public PollMediaType MediaType { get; private set; }
        public string BucketName { get; private set; } = string.Empty;
        public string ObjectKey { get; private set; } = string.Empty;
        public string MimeType { get; private set; } = string.Empty;
        public long? ExpectedFileSize { get; private set; }
        public long? ActualFileSize { get; private set; }
        public long? DurationMs { get; private set; }
        public MediaUploadStatus Status { get; private set; }
        public long SelectionVersion { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }
        public DateTime ExpiresAt { get; private set; }
        public DateTime? AttachedAt { get; private set; }
        public string? FailureReason { get; private set; }
        public DateTime? PurgedAt { get; private set; }

        public User? User { get; private set; }
        public Guest? Guest { get; private set; }
        public Poll? Poll { get; private set; }

        private MediaUploadSession()
        {
        }

        public static MediaUploadSession ForUser(
            Guid userId,
            Guid? pollId,
            PollMediaType mediaType,
            string bucketName,
            string extension,
            string mimeType,
            long? expectedFileSize,
            long selectionVersion,
            DateTime expiresAt)
        {
            var session = CreateCore(
                pollId,
                mediaType,
                bucketName,
                mimeType,
                expectedFileSize,
                selectionVersion,
                expiresAt);
            session.UserId = userId;
            session.ObjectKey = BuildUserObjectKey(userId, session.Id, extension);
            return session;
        }

        public static MediaUploadSession ForGuest(
            Guid guestId,
            PollMediaType mediaType,
            string bucketName,
            string extension,
            string mimeType,
            long? expectedFileSize,
            long selectionVersion,
            DateTime expiresAt)
        {
            var session = CreateCore(
                pollId: null,
                mediaType,
                bucketName,
                mimeType,
                expectedFileSize,
                selectionVersion,
                expiresAt);
            session.GuestId = guestId;
            session.ObjectKey = BuildGuestObjectKey(guestId, session.Id, extension);
            return session;
        }

        public static string BuildUserObjectKey(Guid userId, Guid sessionId, string extension)
        {
            return $"polls/pending/users/{userId:D}/{sessionId:D}{extension}";
        }

        public static string BuildGuestObjectKey(Guid guestId, Guid sessionId, string extension)
        {
            return $"polls/pending/guests/{guestId:D}/{sessionId:D}{extension}";
        }

        /// <summary>Legacy key shape used before guest uploads.</summary>
        public static string BuildObjectKey(Guid userId, Guid sessionId, string extension)
        {
            return $"polls/pending/{userId:D}/{sessionId:D}{extension}";
        }

        internal void ClaimToUser(Guid userId)
        {
            UserId = userId;
            GuestId = null;
            UpdatedAt = DateTime.UtcNow;
        }

        internal void MarkUploaded(long actualFileSize, long? durationMs)
        {
            Status = MediaUploadStatus.Uploaded;
            ActualFileSize = actualFileSize;
            DurationMs = durationMs;
            FailureReason = null;
            UpdatedAt = DateTime.UtcNow;
        }

        internal void MarkFailed(string failureReason)
        {
            Status = MediaUploadStatus.Failed;
            FailureReason = failureReason;
            ExpiresAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        internal void MarkExpired()
        {
            Status = MediaUploadStatus.Expired;
            UpdatedAt = DateTime.UtcNow;
        }

        /// <summary>Invalidates the session immediately so cleanup picks it up on its next run.</summary>
        internal void ExpireNow(DateTime utcNow)
        {
            Status = MediaUploadStatus.Expired;
            ExpiresAt = utcNow;
            UpdatedAt = utcNow;
        }

        internal void MarkCancelled()
        {
            Status = MediaUploadStatus.Cancelled;
            ExpiresAt = DateTime.UtcNow;
            UpdatedAt = DateTime.UtcNow;
        }

        internal void LinkToPoll(Guid pollId)
        {
            PollId = pollId;
            UpdatedAt = DateTime.UtcNow;
        }

        internal void Unlink()
        {
            PollId = null;
            UpdatedAt = DateTime.UtcNow;
        }

        internal void MarkAttached(Guid pollId)
        {
            Status = MediaUploadStatus.Attached;
            PollId = pollId;
            AttachedAt = DateTime.UtcNow;
            FailureReason = null;
            UpdatedAt = DateTime.UtcNow;
        }

        internal void MarkPurged()
        {
            PurgedAt = DateTime.UtcNow;
            FailureReason = null;
            UpdatedAt = DateTime.UtcNow;
        }

        internal void RecordFailure(string failureReason)
        {
            FailureReason = failureReason;
            UpdatedAt = DateTime.UtcNow;
        }

        private static MediaUploadSession CreateCore(
            Guid? pollId,
            PollMediaType mediaType,
            string bucketName,
            string mimeType,
            long? expectedFileSize,
            long selectionVersion,
            DateTime expiresAt)
        {
            var now = DateTime.UtcNow;
            return new MediaUploadSession
            {
                Id = Guid.NewGuid(),
                PollId = pollId,
                MediaType = mediaType,
                BucketName = bucketName,
                MimeType = mimeType,
                ExpectedFileSize = expectedFileSize,
                Status = MediaUploadStatus.Pending,
                SelectionVersion = selectionVersion,
                CreatedAt = now,
                UpdatedAt = now,
                ExpiresAt = expiresAt
            };
        }
    }
}
