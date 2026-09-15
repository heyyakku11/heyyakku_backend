using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class EmailLog
    {
        public const int MaxErrorMessageLength = 1000;

        public Guid Id { get; private set; }
        public Guid? UserId { get; private set; }
        public string RecipientEmail { get; private set; } = string.Empty;
        public EmailType EmailType { get; private set; }
        public Guid ReferenceId { get; private set; }
        public EmailStatus Status { get; private set; }
        public int AttemptCount { get; private set; }
        public int MaxAttempts { get; private set; }
        public string Provider { get; private set; } = string.Empty;
        public string? ProviderMessageId { get; private set; }
        public string? ErrorCode { get; private set; }
        public string? ErrorMessage { get; private set; }
        public DateTime QueuedAt { get; private set; }
        public DateTime? StartedAt { get; private set; }
        public DateTime? SentAt { get; private set; }
        public DateTime? FailedAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User? User { get; private set; }

        private EmailLog()
        {
        }

        public EmailLog(
            string recipientEmail,
            EmailType emailType,
            Guid referenceId,
            string provider,
            int maxAttempts,
            Guid? userId = null)
        {
            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                throw new ArgumentException("Recipient email is required.", nameof(recipientEmail));
            }

            if (string.IsNullOrWhiteSpace(provider))
            {
                throw new ArgumentException("Provider is required.", nameof(provider));
            }

            if (maxAttempts < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(maxAttempts), "MaxAttempts must be at least 1.");
            }

            var now = DateTime.UtcNow;
            Id = Guid.NewGuid();
            UserId = userId;
            RecipientEmail = recipientEmail.Trim().ToLowerInvariant();
            EmailType = emailType;
            ReferenceId = referenceId;
            Status = EmailStatus.Queued;
            AttemptCount = 0;
            MaxAttempts = maxAttempts;
            Provider = provider;
            QueuedAt = now;
            CreatedAt = now;
            UpdatedAt = now;
        }

        public void MarkSending()
        {
            EnsureTransition(EmailStatus.Queued, EmailStatus.Sending);
            Status = EmailStatus.Sending;
            StartedAt ??= DateTime.UtcNow;
            Touch();
        }

        public void RecordAttempt()
        {
            if (Status != EmailStatus.Sending)
            {
                throw new InvalidOperationException(
                    $"Cannot record attempt while EmailLog is in status {Status}.");
            }

            AttemptCount++;
            Touch();
        }

        public void MarkSent(string? providerMessageId)
        {
            EnsureTransition(EmailStatus.Sending, EmailStatus.Sent);
            Status = EmailStatus.Sent;
            ProviderMessageId = Truncate(providerMessageId, 128);
            SentAt = DateTime.UtcNow;
            ErrorCode = null;
            ErrorMessage = null;
            Touch();
        }

        public void MarkFailed(string errorCode, string? errorMessage)
        {
            EnsureTransition(EmailStatus.Sending, EmailStatus.Failed);
            Status = EmailStatus.Failed;
            ErrorCode = Truncate(errorCode, 64);
            ErrorMessage = Truncate(errorMessage, MaxErrorMessageLength);
            FailedAt = DateTime.UtcNow;
            Touch();
        }

        public void MarkCancelled(string errorCode, string? errorMessage = null)
        {
            if (Status is EmailStatus.Sent or EmailStatus.Failed or EmailStatus.Cancelled)
            {
                throw new InvalidOperationException(
                    $"Cannot cancel EmailLog while status is {Status}.");
            }

            Status = EmailStatus.Cancelled;
            ErrorCode = Truncate(errorCode, 64);
            ErrorMessage = Truncate(errorMessage, MaxErrorMessageLength);
            FailedAt = DateTime.UtcNow;
            Touch();
        }

        private void EnsureTransition(EmailStatus expected, EmailStatus target)
        {
            if (Status != expected)
            {
                throw new InvalidOperationException(
                    $"Cannot transition EmailLog from {Status} to {target}; expected {expected}.");
            }
        }

        private void Touch()
        {
            UpdatedAt = DateTime.UtcNow;
        }

        private static string? Truncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength
                ? trimmed
                : trimmed[..maxLength];
        }
    }
}
