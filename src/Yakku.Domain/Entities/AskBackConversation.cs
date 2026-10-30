using Yakku.Domain.Enums;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// A private conversation between a poll or check owner (the asker) and one voter, about that voter's vote.
    /// There is at most one per vote. The two user ids are nullable only so the conversation can outlive an account
    /// that was permanently deleted (the foreign keys set them to null); the other person keeps the history.
    /// The vote is not a foreign key (like votes' voter ids) so a deleted vote does not take the chat with it.
    /// </summary>
    public class AskBackConversation
    {
        public Guid Id { get; private set; }
        public ShareContentType ContentType { get; private set; }

        /// <summary>The poll id, or the made check's (user_checks) id.</summary>
        public Guid ContentId { get; private set; }
        public Guid VoteId { get; private set; }
        public Guid? CreatorUserId { get; private set; }
        public Guid? VoterUserId { get; private set; }
        public AskBackStatus Status { get; private set; }

        /// <summary>Who blocked the conversation; set only while <see cref="Status"/> is Blocked.</summary>
        public Guid? BlockedByUserId { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public DateTime? LastMessageAt { get; private set; }

        /// <summary>Last message, or creation: the sort key of the conversation list.</summary>
        public DateTime LastActivityAt { get; private set; }
        public DateTime? ClosedAt { get; private set; }

        public ICollection<AskBackParticipant> Participants { get; private set; } = new List<AskBackParticipant>();
        public ICollection<AskBackMessage> Messages { get; private set; } = new List<AskBackMessage>();

        private AskBackConversation()
        {
        }

        public static AskBackConversation Create(
            ShareContentType contentType,
            Guid contentId,
            Guid voteId,
            Guid creatorUserId,
            Guid voterUserId,
            DateTime utcNow)
        {
            if (creatorUserId == voterUserId)
            {
                throw new ArgumentException("A user cannot ask themselves back.", nameof(voterUserId));
            }

            var conversation = new AskBackConversation
            {
                Id = Guid.NewGuid(),
                ContentType = contentType,
                ContentId = contentId,
                VoteId = voteId,
                CreatorUserId = creatorUserId,
                VoterUserId = voterUserId,
                Status = AskBackStatus.Active,
                CreatedAt = utcNow,
                LastActivityAt = utcNow
            };
            conversation.Participants.Add(AskBackParticipant.Create(conversation.Id, creatorUserId, utcNow));
            conversation.Participants.Add(AskBackParticipant.Create(conversation.Id, voterUserId, utcNow));
            return conversation;
        }

        public bool IsParticipant(Guid userId)
        {
            return userId != Guid.Empty && (CreatorUserId == userId || VoterUserId == userId);
        }

        /// <summary>The other participant, or null when that account no longer exists.</summary>
        public Guid? OtherParticipant(Guid userId)
        {
            return CreatorUserId == userId ? VoterUserId : CreatorUserId;
        }

        /// <summary>Both accounts exist, so there is still someone to talk to.</summary>
        public bool HasBothParticipants => CreatorUserId is not null && VoterUserId is not null;

        public bool CanSend => Status == AskBackStatus.Active && HasBothParticipants;

        public void RecordMessage(DateTime utcNow)
        {
            LastMessageAt = utcNow;
            LastActivityAt = utcNow;
        }

        /// <summary>Either participant can close an active conversation. False when it is not active.</summary>
        public bool TryClose(DateTime utcNow)
        {
            if (Status != AskBackStatus.Active)
            {
                return false;
            }

            Status = AskBackStatus.Closed;
            ClosedAt = utcNow;
            return true;
        }

        /// <summary>Only the asker can reopen a closed (not blocked) conversation.</summary>
        public bool TryReopen(Guid userId)
        {
            if (Status != AskBackStatus.Closed || CreatorUserId != userId)
            {
                return false;
            }

            Status = AskBackStatus.Active;
            ClosedAt = null;
            return true;
        }

        /// <summary>A participant blocks the other. Blocking wins over closing; false when already blocked.</summary>
        public bool TryBlock(Guid userId, DateTime utcNow)
        {
            if (Status == AskBackStatus.Blocked || !IsParticipant(userId))
            {
                return false;
            }

            Status = AskBackStatus.Blocked;
            BlockedByUserId = userId;
            ClosedAt ??= utcNow;
            return true;
        }

        /// <summary>Only whoever blocked can lift it; the conversation goes back to active.</summary>
        public bool TryUnblock(Guid userId)
        {
            if (Status != AskBackStatus.Blocked || BlockedByUserId != userId)
            {
                return false;
            }

            Status = AskBackStatus.Active;
            BlockedByUserId = null;
            ClosedAt = null;
            return true;
        }
    }
}
