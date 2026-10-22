using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    public class Poll
    {
        public Guid Id { get; private set; }
        public Guid CreatorId { get; private set; }

        /// <summary>Unique, auto-generated code used in shared links instead of the poll id.</summary>
        public string ShareCode { get; private set; } = string.Empty;
        public string Question { get; private set; } = string.Empty;
        public PollContentType ContentType { get; private set; }
        public PollStatus Status { get; private set; }
        public PollVisibility Visibility { get; private set; }
        public DateTime? ExpiresAt { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime UpdatedAt { get; private set; }

        public User Creator { get; private set; } = null!;
        public ICollection<PollOption> Options { get; private set; } = new List<PollOption>();
        public PollMedia? Media { get; private set; }
        public ICollection<PollVote> Votes { get; private set; } = new List<PollVote>();
        public ICollection<SavedPoll> SavedPolls { get; private set; } = new List<SavedPoll>();
        public ICollection<MediaUploadSession> UploadSessions { get; private set; } = new List<MediaUploadSession>();

        private Poll()
        {
        }

        public Poll(
            Guid creatorId,
            string question,
            PollContentType contentType,
            PollVisibility visibility = PollVisibility.Public,
            DateTime? expiresAt = null)
        {
            Id = Guid.NewGuid();
            CreatorId = creatorId;
            Question = question;
            ContentType = contentType;
            Status = PollStatus.Draft;
            Visibility = visibility;
            ExpiresAt = expiresAt;
            CreatedAt = DateTime.UtcNow;
            ShareCode = ShareCodeGenerator.New(creatorId, CreatedAt);
            UpdatedAt = CreatedAt;
        }

        public void AddTextOption(string text, int sortOrder)
        {
            Options.Add(PollOption.CreateText(Id, text, sortOrder));
        }

        public string? GetMediaAttachmentBlocker(PollMediaType mediaType)
        {
            if (Status != PollStatus.Draft)
            {
                return "Media can only be attached to a draft poll.";
            }

            if (ContentType == PollContentType.Question)
            {
                return "Question polls cannot have media.";
            }

            if (Media is not null)
            {
                return "This poll already has media.";
            }

            if (!ContentTypeMatches(mediaType))
            {
                return "Media type does not match the poll content type.";
            }

            return null;
        }

        public void AttachMedia(PollMedia media)
        {
            var blocker = GetMediaAttachmentBlocker(media.MediaType);
            if (blocker is not null)
            {
                throw new InvalidOperationException(blocker);
            }

            Media = media;
            UpdatedAt = DateTime.UtcNow;
        }

        public PollMedia? ClearMedia()
        {
            if (Status != PollStatus.Draft)
            {
                throw new InvalidOperationException("Media can only be changed on a draft poll.");
            }

            var existing = Media;
            Media = null;
            UpdatedAt = DateTime.UtcNow;
            return existing;
        }

        public void UpdateQuestion(string question)
        {
            if (Status != PollStatus.Draft)
            {
                throw new InvalidOperationException("Only a draft poll can be updated.");
            }

            Question = question;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateVisibility(PollVisibility visibility)
        {
            if (Status != PollStatus.Draft)
            {
                throw new InvalidOperationException("Only a draft poll can be updated.");
            }

            Visibility = visibility;
            UpdatedAt = DateTime.UtcNow;
        }

        public void UpdateExpiresAt(DateTime? expiresAt)
        {
            if (Status != PollStatus.Draft)
            {
                throw new InvalidOperationException("Only a draft poll can be updated.");
            }

            ExpiresAt = expiresAt;
            UpdatedAt = DateTime.UtcNow;
        }

        public void ReplaceTextOptions(IReadOnlyList<(string Text, int DisplayOrder)> options)
        {
            if (Status != PollStatus.Draft)
            {
                throw new InvalidOperationException("Only a draft poll can be updated.");
            }

            Options.Clear();
            foreach (var option in options)
            {
                AddTextOption(option.Text, option.DisplayOrder);
            }

            UpdatedAt = DateTime.UtcNow;
        }

        public string? GetPublishBlocker()
        {
            if (Status != PollStatus.Draft)
            {
                return "Only a draft poll can be published.";
            }

            if (string.IsNullOrWhiteSpace(Question))
            {
                return "Question is required.";
            }

            if (Options.Count is < 2 or > 3)
            {
                return "A poll must have 2 or 3 options.";
            }

            if (ContentType == PollContentType.Question)
            {
                return Media is null
                    ? null
                    : "Question polls cannot have media.";
            }

            if (Media is null || !ContentTypeMatches(Media.MediaType))
            {
                return "This poll needs one matching media item before it can be published.";
            }

            return null;
        }

        public void Publish()
        {
            var blocker = GetPublishBlocker();
            if (blocker is not null)
            {
                throw new InvalidOperationException(blocker);
            }

            MarkPublished();
        }

        public void MarkPublished()
        {
            Status = PollStatus.Active;
            UpdatedAt = DateTime.UtcNow;
        }

        public bool TryClose()
        {
            if (Status != PollStatus.Active)
            {
                return false;
            }

            Status = PollStatus.Closed;
            UpdatedAt = DateTime.UtcNow;
            return true;
        }

        public bool IsAcceptingVotes(DateTime utcNow)
        {
            if (Status != PollStatus.Active)
            {
                return false;
            }

            return ExpiresAt is null || ExpiresAt > utcNow;
        }

        public bool MatchesMedia(PollMediaType mediaType)
        {
            return ContentTypeMatches(mediaType);
        }

        private bool ContentTypeMatches(PollMediaType mediaType)
        {
            return ContentType switch
            {
                PollContentType.Image => mediaType == PollMediaType.Image,
                PollContentType.Video => mediaType == PollMediaType.Video,
                PollContentType.Audio => mediaType == PollMediaType.Audio,
                _ => false
            };
        }
    }
}
