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

        /// <summary>Messages the asker sent. A concurrency token, so two sends at once cannot both pass the turn rule.</summary>
        public int AskerMessageCount { get; private set; }

        /// <summary>Messages the voter sent. A concurrency token, like <see cref="AskerMessageCount"/>.</summary>
        public int VoterMessageCount { get; private set; }

        /// <summary>
        /// Asker sends already counted under a prior (lower) entitlement. Budget math uses
        /// <c>AskerMessageCount - AskerQuotaOffset</c> so raising the plan limit grants a full new budget.
        /// </summary>
        public int AskerQuotaOffset { get; private set; }

        /// <summary>
        /// The asker <c>MessageLimit</c> this conversation last aligned its quota offset to.
        /// Null until the first finite limit is applied (or after an unlimited plan clears it).
        /// </summary>
        public int? AppliedMessageLimit { get; private set; }

        /// <summary>Extra asker questions bought with conversation top-ups (sum of the granted credits).</summary>
        public int TopUpGranted { get; private set; }

        /// <summary>Top-up credits already spent: asker sends made after the plan limit was used up.</summary>
        public int TopUpUsed { get; private set; }

        /// <summary>Top-up credits still available.</summary>
        public int TopUpCreditsLeft => Math.Max(0, TopUpGranted - TopUpUsed);

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

        /// <summary>Active and both people exist. Whose turn it is and the plan's limits are checked on top of this.</summary>
        public bool CanSend => Status == AskBackStatus.Active && HasBothParticipants;

        public void RecordMessage(DateTime utcNow)
        {
            LastMessageAt = utcNow;
            LastActivityAt = utcNow;
        }

        /// <summary>
        /// Asker questions that count against the current entitlement (excludes sends grandfathered by
        /// <see cref="AskerQuotaOffset"/> when the limit was raised).
        /// </summary>
        public int EffectiveAskerSent => Math.Max(0, AskerMessageCount - AskerQuotaOffset);

        /// <summary>
        /// Gives the asker a full new budget under <paramref name="limit"/>: every send made so far is grandfathered.
        /// Used only when the asker buys a user plan (Lite or Love), never by reading a conversation, so a plan that
        /// expires or a limit that drops does not hand out questions. Top-up credits are kept.
        /// </summary>
        public void ResetAskerBudget(int? limit)
        {
            AskerQuotaOffset = AskerMessageCount;
            AppliedMessageLimit = limit;
        }

        /// <summary>Adds top-up credits in memory (the database grant does the same with an atomic update).</summary>
        public void AddTopUpCredits(int credits)
        {
            if (credits <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(credits));
            }

            TopUpGranted += credits;
        }

        /// <summary>
        /// Turn rule: the asker sends first, the voter answers once, then it is the asker's turn again. The asker may
        /// send <paramref name="limit"/> questions in all (null means no limit), so the voter answers at most that many
        /// times. When every allowed question was answered the conversation is finished.
        /// </summary>
        public AskBackTurn TurnFor(Guid userId, int? limit)
        {
            var isAsker = CreatorUserId == userId;
            if (IsFinished(limit))
            {
                return AskBackTurn.Done;
            }

            var askerWaiting = AskerMessageCount > VoterMessageCount;
            return isAsker == !askerWaiting ? AskBackTurn.Me : AskBackTurn.Them;
        }

        /// <summary>
        /// Asker questions still allowed: what is left under <paramref name="limit"/> plus unused top-up credits.
        /// Always null for the voter (no personal quota) and when there is no limit.
        /// </summary>
        public int? MessagesLeft(Guid userId, int? limit)
        {
            if (CreatorUserId != userId || limit is not { } max)
            {
                return null;
            }

            return Math.Max(0, max - EffectiveAskerSent) + TopUpCreditsLeft;
        }

        /// <summary>
        /// Counts a message from <paramref name="userId"/> if the turn rule allows it, and records the activity.
        /// Anything but <see cref="AskBackSendResult.Ok"/> leaves the conversation unchanged.
        /// </summary>
        public AskBackSendResult TryRecordMessage(Guid userId, int? limit, DateTime utcNow)
        {
            if (IsFinished(limit))
            {
                return AskBackSendResult.LimitReached;
            }

            var isAsker = CreatorUserId == userId;
            var askerWaiting = AskerMessageCount > VoterMessageCount;
            if (isAsker == askerWaiting)
            {
                return AskBackSendResult.NotYourTurn;
            }

            if (isAsker)
            {
                // Past the plan limit, a question spends one top-up credit. The offset moves with it, so the plan
                // part of the budget stays exhausted and the credit is the only thing that grows the allowance.
                if (limit is { } max && EffectiveAskerSent >= max)
                {
                    if (TopUpCreditsLeft == 0)
                    {
                        return AskBackSendResult.LimitReached;
                    }

                    TopUpUsed++;
                    AskerQuotaOffset++;
                }

                AskerMessageCount++;
            }
            else
            {
                VoterMessageCount++;
            }

            RecordMessage(utcNow);
            return AskBackSendResult.Ok;
        }

        private bool IsFinished(int? limit)
        {
            return limit is { } max
                   && EffectiveAskerSent >= max
                   && TopUpCreditsLeft == 0
                   && VoterMessageCount >= AskerMessageCount;
        }

        /// <summary>
        /// Why the asker cannot top this conversation up right now, or null when they can. Only the asker can top up an
        /// active conversation with both people still here, on a card that is unlocked and not paused, with a finite
        /// limit whose budget (plan part and credits) is used up. A pending voter reply does not block a top-up.
        /// </summary>
        public string? TopUpBlockReason(Guid userId, int? limit, bool unlocked, bool paused)
        {
            if (CreatorUserId != userId)
            {
                return "Only the person who asked can add messages to this conversation.";
            }

            if (Status == AskBackStatus.Blocked)
            {
                return "This conversation is blocked, so it cannot be topped up.";
            }

            if (Status == AskBackStatus.Closed)
            {
                return "Reopen this conversation before adding more messages.";
            }

            if (!HasBothParticipants)
            {
                return "The other person is no longer on Yakku.";
            }

            if (!unlocked)
            {
                return "Unlock this card to continue this conversation.";
            }

            if (paused)
            {
                return "Your plan has expired. Buy Yakku Lite or Yakku Love to continue.";
            }

            if (limit is null)
            {
                return "This conversation has no message limit to top up.";
            }

            return MessagesLeft(userId, limit) > 0
                ? "You still have messages left in this conversation."
                : null;
        }

        /// <summary>
        /// The asker can continue this conversation by buying Lite or Love: the card is unlocked, the chat is not blocked,
        /// and either the budget is used up or the card is paused. Blocked chats are never continued this way.
        /// </summary>
        public bool CanContinueWithPlan(Guid userId, int? limit, bool unlocked, bool paused)
        {
            if (CreatorUserId != userId || Status == AskBackStatus.Blocked || !unlocked)
            {
                return false;
            }

            if (paused)
            {
                return true;
            }

            return limit is { } && MessagesLeft(userId, limit) == 0;
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
