using Yakku.Application.AskBack;
using Yakku.Application.Common.Exceptions;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class AskBackDomainTests
{
    private static readonly DateTime Now = new(2026, 10, 30, 12, 0, 0, DateTimeKind.Utc);

    private static AskBackConversation NewConversation(Guid? asker = null, Guid? voter = null)
    {
        return AskBackConversation.Create(
            ShareContentType.Poll, Guid.NewGuid(), Guid.NewGuid(), asker ?? Guid.NewGuid(), voter ?? Guid.NewGuid(), Now);
    }

    [Fact]
    public void Create_MakesAnActiveConversationWithExactlyTwoParticipants()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();

        var conversation = NewConversation(asker, voter);

        Assert.Equal(AskBackStatus.Active, conversation.Status);
        Assert.Equal(2, conversation.Participants.Count);
        Assert.Contains(conversation.Participants, p => p.UserId == asker);
        Assert.Contains(conversation.Participants, p => p.UserId == voter);
        Assert.True(conversation.CanSend);
        Assert.Equal(Now, conversation.LastActivityAt);
        Assert.Null(conversation.LastMessageAt);
    }

    [Fact]
    public void Create_RefusesAConversationWithOneself()
    {
        var user = Guid.NewGuid();

        Assert.Throws<ArgumentException>(() => NewConversation(user, user));
    }

    [Fact]
    public void IsParticipant_IsTrueOnlyForTheTwoUsers()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);

        Assert.True(conversation.IsParticipant(asker));
        Assert.True(conversation.IsParticipant(voter));
        Assert.False(conversation.IsParticipant(Guid.NewGuid()));
        Assert.False(conversation.IsParticipant(Guid.Empty));
        Assert.Equal(voter, conversation.OtherParticipant(asker));
        Assert.Equal(asker, conversation.OtherParticipant(voter));
    }

    [Fact]
    public void Close_ByEitherParticipant_MakesItReadOnly_AndOnlyTheAskerCanReopen()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);

        Assert.True(conversation.TryClose(Now));
        Assert.Equal(AskBackStatus.Closed, conversation.Status);
        Assert.False(conversation.CanSend);
        Assert.False(conversation.TryClose(Now));

        Assert.False(conversation.TryReopen(voter));
        Assert.True(conversation.TryReopen(asker));
        Assert.Equal(AskBackStatus.Active, conversation.Status);
        Assert.Null(conversation.ClosedAt);
        Assert.True(conversation.CanSend);
    }

    [Fact]
    public void Block_IsSeparateFromClose_AndOnlyTheBlockerCanLiftIt()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);

        Assert.True(conversation.TryBlock(voter, Now));
        Assert.Equal(AskBackStatus.Blocked, conversation.Status);
        Assert.Equal(voter, conversation.BlockedByUserId);
        Assert.False(conversation.CanSend);

        // The asker cannot reopen a block, close it, or lift it.
        Assert.False(conversation.TryReopen(asker));
        Assert.False(conversation.TryClose(Now));
        Assert.False(conversation.TryUnblock(asker));
        Assert.False(conversation.TryBlock(asker, Now));

        Assert.True(conversation.TryUnblock(voter));
        Assert.Equal(AskBackStatus.Active, conversation.Status);
        Assert.Null(conversation.BlockedByUserId);
    }

    [Fact]
    public void Block_ByAStranger_IsRefused()
    {
        var conversation = NewConversation();

        Assert.False(conversation.TryBlock(Guid.NewGuid(), Now));
        Assert.Equal(AskBackStatus.Active, conversation.Status);
    }

    [Fact]
    public void Block_OfAClosedConversation_WinsOverClosing()
    {
        var asker = Guid.NewGuid();
        var conversation = NewConversation(asker);
        conversation.TryClose(Now);

        Assert.True(conversation.TryBlock(asker, Now.AddMinutes(1)));

        Assert.Equal(AskBackStatus.Blocked, conversation.Status);
        Assert.True(conversation.TryUnblock(asker));
        Assert.Equal(AskBackStatus.Active, conversation.Status);
    }

    [Fact]
    public void RecordMessage_MovesTheListSortKey()
    {
        var conversation = NewConversation();
        var later = Now.AddMinutes(5);

        conversation.RecordMessage(later);

        Assert.Equal(later, conversation.LastMessageAt);
        Assert.Equal(later, conversation.LastActivityAt);
    }

    [Fact]
    public void Turns_AskerFirst_ThenVoterReplies_ThenTheAskerAgain()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);

        Assert.Equal(AskBackTurn.Me, conversation.TurnFor(asker, 3));
        Assert.Equal(AskBackTurn.Them, conversation.TurnFor(voter, 3));
        Assert.Equal(AskBackSendResult.NotYourTurn, conversation.TryRecordMessage(voter, 3, Now));

        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(asker, 3, Now));
        Assert.Equal(AskBackTurn.Them, conversation.TurnFor(asker, 3));
        Assert.Equal(AskBackTurn.Me, conversation.TurnFor(voter, 3));
        Assert.Equal(AskBackSendResult.NotYourTurn, conversation.TryRecordMessage(asker, 3, Now));

        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(voter, 3, Now));
        Assert.Equal(AskBackTurn.Me, conversation.TurnFor(asker, 3));
        Assert.Equal(AskBackTurn.Them, conversation.TurnFor(voter, 3));
    }

    [Fact]
    public void Limit_EndsTheChat_AfterTheLastReply_AndCountsWhatIsLeft()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);

        Assert.Equal(1, conversation.MessagesLeft(asker, 1));
        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(asker, 1, Now));
        Assert.Equal(0, conversation.MessagesLeft(asker, 1));

        // The voter may still answer the last question; only then is the chat done.
        Assert.Equal(AskBackTurn.Me, conversation.TurnFor(voter, 1));
        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(voter, 1, Now));

        Assert.Equal(AskBackTurn.Done, conversation.TurnFor(asker, 1));
        Assert.Equal(AskBackTurn.Done, conversation.TurnFor(voter, 1));
        Assert.Equal(AskBackSendResult.LimitReached, conversation.TryRecordMessage(asker, 1, Now));
        Assert.Equal(AskBackSendResult.LimitReached, conversation.TryRecordMessage(voter, 1, Now));
        // Voters have no personal ask-back budget.
        Assert.Null(conversation.MessagesLeft(voter, 1));
    }

    [Fact]
    public void NoLimit_NeverEndsTheChat_AndHasNoMessagesLeftCount()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);

        for (var round = 0; round < 20; round++)
        {
            Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(asker, null, Now));
            Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(voter, null, Now));
        }

        Assert.Equal(AskBackTurn.Me, conversation.TurnFor(asker, null));
        Assert.Null(conversation.MessagesLeft(asker, null));
    }

    [Fact]
    public void PlanPurchase_ResetsTheBudget_WithAFullNewBudget()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);
        conversation.TryRecordMessage(asker, 1, Now);
        conversation.TryRecordMessage(voter, 1, Now);
        Assert.Equal(AskBackTurn.Done, conversation.TurnFor(asker, 1));

        // Buying a plan grandfathers every send made so far: the asker gets the full new limit.
        conversation.ResetAskerBudget(3);
        Assert.Equal(AskBackTurn.Me, conversation.TurnFor(asker, 3));
        Assert.Equal(3, conversation.MessagesLeft(asker, 3));
        Assert.Null(conversation.MessagesLeft(voter, 3));
    }

    [Fact]
    public void LosingThePlan_DoesNotHandOutQuestionsBack()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);
        for (var round = 0; round < 3; round++)
        {
            Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(asker, 3, Now));
            Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(voter, 3, Now));
        }

        // A lower limit (plan expired or downgraded) does not reopen the chat or refill the budget.
        Assert.Equal(AskBackTurn.Done, conversation.TurnFor(asker, 1));
        Assert.Equal(0, conversation.MessagesLeft(asker, 1));
    }

    [Fact]
    public void TopUpCredits_LetTheAskerSendPastTheLimit_AndSpendOneEach()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);
        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(asker, 1, Now));
        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(voter, 1, Now));
        Assert.Equal(AskBackTurn.Done, conversation.TurnFor(asker, 1));

        conversation.AddTopUpCredits(2);

        Assert.Equal(AskBackTurn.Me, conversation.TurnFor(asker, 1));
        Assert.Equal(2, conversation.MessagesLeft(asker, 1));
        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(asker, 1, Now));
        Assert.Equal(1, conversation.TopUpCreditsLeft);
        Assert.Equal(1, conversation.MessagesLeft(asker, 1));
        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(voter, 1, Now));
        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(asker, 1, Now));
        Assert.Equal(0, conversation.MessagesLeft(asker, 1));
        Assert.Equal(AskBackSendResult.Ok, conversation.TryRecordMessage(voter, 1, Now));
        Assert.Equal(AskBackTurn.Done, conversation.TurnFor(asker, 1));
        Assert.Equal(AskBackSendResult.LimitReached, conversation.TryRecordMessage(asker, 1, Now));
    }

    [Fact]
    public void TopUpCredits_SurviveAPlanBudgetReset()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);
        conversation.TryRecordMessage(asker, 1, Now);
        conversation.TryRecordMessage(voter, 1, Now);
        conversation.AddTopUpCredits(2);

        conversation.ResetAskerBudget(3);

        Assert.Equal(2, conversation.TopUpCreditsLeft);
        Assert.Equal(5, conversation.MessagesLeft(asker, 3));
    }

    [Fact]
    public void TopUpBlockReason_AllowsOnlyTheAskerOnceTheBudgetIsUsedUp()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);
        Assert.NotNull(conversation.TopUpBlockReason(asker, 1, true, false));

        // The last question still waiting for a reply already counts as used.
        conversation.TryRecordMessage(asker, 1, Now);
        Assert.Null(conversation.TopUpBlockReason(asker, 1, true, false));

        Assert.NotNull(conversation.TopUpBlockReason(voter, 1, true, false));
        Assert.NotNull(conversation.TopUpBlockReason(asker, 1, false, false));
        Assert.NotNull(conversation.TopUpBlockReason(asker, 1, true, true));
        Assert.NotNull(conversation.TopUpBlockReason(asker, null, true, false));
    }

    [Fact]
    public void TopUpBlockReason_RefusesBlockedAndClosedChats_UntilReopened()
    {
        var asker = Guid.NewGuid();
        var voter = Guid.NewGuid();
        var conversation = NewConversation(asker, voter);
        conversation.TryRecordMessage(asker, 1, Now);

        Assert.True(conversation.TryBlock(voter, Now));
        Assert.NotNull(conversation.TopUpBlockReason(asker, 1, true, false));
        Assert.True(conversation.TryUnblock(voter));

        Assert.True(conversation.TryClose(Now));
        Assert.NotNull(conversation.TopUpBlockReason(asker, 1, true, false));
        Assert.True(conversation.TryReopen(asker));
        Assert.Null(conversation.TopUpBlockReason(asker, 1, true, false));
    }

    [Fact]
    public void ActivatePlan_SetsAnExpiry_ForAPlanWithADuration_AndClearsItForFree()
    {
        var user = new User("5710000000001", "plan-anon-1");
        var lite = Plan.Create(Plan.YakkuLiteId, Plan.YakkuLiteCode, "Yakku Lite", durationDays: 7, unlockTickets: 3, askBackMessageLimit: 3);
        var free = Plan.Create(Plan.FreeId, Plan.FreeCode, "Free", durationDays: null, unlockTickets: 0, askBackMessageLimit: 1);

        user.ActivatePlan(lite, Now);

        Assert.Equal(Plan.YakkuLiteId, user.PlanId);
        Assert.Equal(Now, user.PlanActivatedAt);
        Assert.Equal(Now.AddDays(7), user.PlanExpiresAt);
        Assert.True(user.IsPlanActive(Now.AddDays(6)));
        Assert.False(user.IsPlanActive(Now.AddDays(7).AddSeconds(1)));

        // Buying it again restarts the period from that moment.
        user.ActivatePlan(lite, Now.AddDays(5));
        Assert.Equal(Now.AddDays(12), user.PlanExpiresAt);

        user.ActivatePlan(free, Now.AddDays(6));
        Assert.Null(user.PlanActivatedAt);
        Assert.Null(user.PlanExpiresAt);
        Assert.True(user.IsPlanActive(Now.AddYears(5)));
    }

    [Fact]
    public void PlanUpdate_MapsMinusOneToUnlimited_AndLeavesUntouchedFieldsAlone()
    {
        var plan = Plan.Create(Plan.YakkuLiteId, Plan.YakkuLiteCode, "Yakku Lite", durationDays: 7, unlockTickets: 3, askBackMessageLimit: 3);

        plan.Update(name: null, priceMinor: null, isActive: null, unlockTickets: Plan.Unlimited);
        Assert.Null(plan.UnlockTickets);
        Assert.Equal(3, plan.AskBackMessageLimit);
        Assert.Equal(7, plan.DurationDays);

        plan.Update(name: null, priceMinor: null, isActive: null, durationDays: 14, askBackMessageLimit: Plan.Unlimited);
        Assert.Null(plan.AskBackMessageLimit);
        Assert.Equal(14, plan.DurationDays);
    }

    [Fact]
    public void CardUnlock_IsForPollsAndChecksOnly_AndAPurchaseNeverGoesBackToAPlanUnlock()
    {
        var owner = Guid.NewGuid();
        Assert.Throws<ArgumentException>(() => CardUnlock.Create(
            owner, ShareContentType.Personality, Guid.NewGuid(), CardUnlockSource.Plan, Now));

        var unlock = CardUnlock.Create(owner, ShareContentType.Poll, Guid.NewGuid(), CardUnlockSource.Plan, Now);
        Assert.True(unlock.PromoteToPurchase());
        Assert.Equal(CardUnlockSource.Purchase, unlock.Source);
        Assert.False(unlock.PromoteToPurchase());
    }

    [Fact]
    public void Participant_ReadPosition_OnlyMovesForward()
    {
        var conversation = NewConversation();
        var participant = conversation.Participants.First();
        var first = (Id: Guid.NewGuid(), At: Now);
        var second = (Id: Guid.NewGuid(), At: Now.AddSeconds(1));

        Assert.True(participant.TryAdvanceRead(second.Id, second.At, Now));
        Assert.False(participant.TryAdvanceRead(first.Id, first.At, Now));
        Assert.False(participant.TryAdvanceRead(second.Id, second.At, Now));

        Assert.Equal(second.Id, participant.LastReadMessageId);
        Assert.True(participant.TryAdvanceRead(Guid.NewGuid(), second.At.AddSeconds(1), Now));
    }

    [Fact]
    public void Participant_ReadPosition_BreaksTiesByIdInUuidOrder()
    {
        var conversation = NewConversation();
        var participant = conversation.Participants.First();
        var lower = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var higher = Guid.Parse("00000000-0000-0000-0000-000000000002");

        Assert.True(participant.TryAdvanceRead(lower, Now, Now));
        Assert.True(participant.TryAdvanceRead(higher, Now, Now));
        Assert.False(participant.TryAdvanceRead(lower, Now, Now));
    }

    [Fact]
    public void Cursor_RoundTrips_AndRejectsGarbage()
    {
        var id = Guid.NewGuid();

        var decoded = AskBackCursor.TryDecode(AskBackCursor.Encode(Now, id));

        Assert.Equal((Now, id), decoded);
        Assert.Null(AskBackCursor.TryDecode(null));
        Assert.Null(AskBackCursor.TryDecode("  "));
        Assert.Throws<AppException>(() => AskBackCursor.TryDecode("not-base64!"));
        Assert.Throws<AppException>(() => AskBackCursor.TryDecode(Convert.ToBase64String("nope"u8.ToArray())));
    }
}
