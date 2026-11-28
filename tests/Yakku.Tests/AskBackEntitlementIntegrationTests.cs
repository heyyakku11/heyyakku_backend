using Microsoft.EntityFrameworkCore;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Services;
using Yakku.Application.Checks.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls;
using Yakku.Application.Sharing;
using Yakku.Application.Sharing.DTOs;
using Yakku.Application.Sharing.Services;
using Yakku.Application.Sharing.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>
/// Real-PostgreSQL checks for who may ask back (locked free plan, single-card unlock, plans, expiry), the turn rule and
/// the per-plan message limits, and the user-level block.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class AskBackEntitlementIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public AskBackEntitlementIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---- a card bought on its own, or locked ----

    [SkippableFact]
    public async Task Free_CannotStartAConversation_OnAnyLockedCard_EvenItsTopReason()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await AskBackIntegrationTests.SceneAsync(context);
        var service = AskBackIntegrationTests.ServiceFor(context);

        // The free plan has no questions: the reason is locked even though it is the top reason of its option.
        var locked = await Assert.ThrowsAsync<AppException>(() => service.CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id }));
        Assert.Equal(403, locked.StatusCode);
        Assert.Equal(ApiErrorCodes.AskBackLocked, locked.ErrorCode);
        Assert.Equal(0, await context.AskBackConversations.CountAsync(c => c.VoteId == scene.Vote.Id));
    }

    [SkippableFact]
    public async Task ThisCard_AllowsOneAskBack_OneReply_ThenTheChatIsDone()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await AskBackIntegrationTests.SceneAsync(context);
        var service = AskBackIntegrationTests.ServiceFor(context);

        // A card bought on its own (this_card) gives one question per reason.
        await AskBackIntegrationTests.EntitlementsFor(context).GrantCardUnlockAsync(scene.Asker.Id, "poll", scene.Poll.Id);
        var (conversation, _) = await service.CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id });
        var conversationId = conversation.Id;

        var asAsker = await service.GetConversationAsync(scene.Asker.Id, conversationId);
        var asVoter = await service.GetConversationAsync(scene.Voter.Id, conversationId);
        Assert.Equal(("me", true, (int?)1), (asAsker.Turn, asAsker.CanSend, asAsker.MessagesLeft));
        // Voters have no ask-back message budget; they only reply on their turn.
        Assert.Equal(("them", false, (int?)null), (asVoter.Turn, asVoter.CanSend, asVoter.MessagesLeft));

        // The asker speaks first.
        var early = await Assert.ThrowsAsync<AppException>(() => service.SendMessageAsync(
            scene.Voter.Id, conversationId, AskBackIntegrationTests.Send("Hi first")));
        Assert.Equal(409, early.StatusCode);
        Assert.Equal(ApiErrorCodes.AskBackNotYourTurn, early.ErrorCode);

        await service.SendMessageAsync(scene.Asker.Id, conversationId, AskBackIntegrationTests.Send("Why?"));

        // One question, then the voter's turn: asking again waits.
        var twice = await Assert.ThrowsAsync<AppException>(() => service.SendMessageAsync(
            scene.Asker.Id, conversationId, AskBackIntegrationTests.Send("Hello?")));
        Assert.Equal(ApiErrorCodes.AskBackNotYourTurn, twice.ErrorCode);
        asAsker = await service.GetConversationAsync(scene.Asker.Id, conversationId);
        Assert.Equal(("them", false, (int?)0), (asAsker.Turn, asAsker.CanSend, asAsker.MessagesLeft));

        await service.SendMessageAsync(scene.Voter.Id, conversationId, AskBackIntegrationTests.Send("Because."));

        // Every allowed question is answered: the conversation is done for both.
        var askerDone = await service.GetConversationAsync(scene.Asker.Id, conversationId);
        Assert.Equal("done", askerDone.Turn);
        Assert.False(askerDone.CanSend);
        Assert.Equal(0, askerDone.MessagesLeft);
        var voterDone = await service.GetConversationAsync(scene.Voter.Id, conversationId);
        Assert.Equal("done", voterDone.Turn);
        Assert.False(voterDone.CanSend);
        Assert.Null(voterDone.MessagesLeft);

        foreach (var user in new[] { scene.Asker.Id, scene.Voter.Id })
        {
            var error = await Assert.ThrowsAsync<AppException>(() => service.SendMessageAsync(
                user, conversationId, AskBackIntegrationTests.Send("one more")));
            Assert.Equal(409, error.StatusCode);
            Assert.Equal(ApiErrorCodes.AskBackLimitReached, error.ErrorCode);
        }

        Assert.Equal(2, await context.AskBackMessages.CountAsync(m => m.ConversationId == conversationId));
    }

    [SkippableFact]
    public async Task ThisCardUsed_ThenLite_ContinuesTheChat_WithAFreshLiteBudget_AndNoSecondUnlock()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await AskBackIntegrationTests.SceneAsync(context);
        var service = AskBackIntegrationTests.ServiceFor(context);
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);

        // The card was bought on its own (this_card): one question per reason.
        await entitlements.GrantCardUnlockAsync(scene.Asker.Id, "poll", scene.Poll.Id);
        var (conversation, _) = await service.CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id });
        var conversationId = conversation.Id;

        await service.SendMessageAsync(scene.Asker.Id, conversationId, AskBackIntegrationTests.Send("Free Q"));
        await service.SendMessageAsync(scene.Voter.Id, conversationId, AskBackIntegrationTests.Send("Free A"));
        Assert.Equal("done", (await service.GetConversationAsync(scene.Asker.Id, conversationId)).Turn);

        // Buying Lite continues the same chat with a fresh Lite budget (3 per reason); the card is not unlocked again.
        await AskBackIntegrationTests.ActivatePlanAsync(context, scene.Asker.Id, Plan.YakkuLiteCode);

        var after = await service.GetConversationAsync(scene.Asker.Id, conversationId);
        Assert.Equal("me", after.Turn);
        Assert.Equal(3, after.MessagesLeft);
        Assert.True(after.CanSend);
        Assert.Null((await service.GetConversationAsync(scene.Voter.Id, conversationId)).MessagesLeft);
    }

    [SkippableFact]
    public async Task ConcurrentQuestions_FromTheAsker_StoreOnlyOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var (scene, conversationId) = await AskBackIntegrationTests.StartedAsync(setup);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var context = _fixture.CreateContext();
            try
            {
                await AskBackIntegrationTests.ServiceFor(context).SendMessageAsync(
                    scene.Asker.Id, conversationId, AskBackIntegrationTests.Send("Why?"));
                return (string?)null;
            }
            catch (AppException exception)
            {
                return exception.ErrorCode;
            }
        }));

        Assert.Equal(1, outcomes.Count(code => code is null));
        Assert.All(outcomes.Where(code => code is not null), code => Assert.Equal(ApiErrorCodes.AskBackNotYourTurn, code));
        Assert.Equal(1, await setup.AskBackMessages.CountAsync(m => m.ConversationId == conversationId));
    }

    // ---- unlocking a card ----

    [SkippableFact]
    public async Task SingleCardUnlock_OpensEveryReason_WithTheFreeQuestionLimit_AndNeverExpires()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await AskBackIntegrationTests.SceneAsync(context);
        var newer = await AddVoteAsync(context, scene.Poll, scene.Poll.Options.First().Id, "A newer reason");
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);
        var service = AskBackIntegrationTests.ServiceFor(context);

        var granted = await entitlements.GrantCardUnlockAsync(scene.Asker.Id, "poll", scene.Poll.Id);
        Assert.Equal("purchase", granted.Source);
        var access = await entitlements.GetAccessAsync(scene.Asker.Id, scene.Vote.Id);
        Assert.True(access.Unlocked);
        Assert.Equal("purchase", access.UnlockSource);
        Assert.Equal(1, access.MessageLimit);

        // Both the old reason and the new one can be asked about: the card is open.
        var (first, _) = await service.CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id });
        var (second, _) = await service.CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = newer.Vote.Id });
        Assert.NotEqual(first.Id, second.Id);

        // Each reason still gets one question.
        await service.SendMessageAsync(scene.Asker.Id, first.Id, AskBackIntegrationTests.Send("Why?"));
        await service.SendMessageAsync(scene.Voter.Id, first.Id, AskBackIntegrationTests.Send("Because."));
        var limit = await Assert.ThrowsAsync<AppException>(() => service.SendMessageAsync(
            scene.Asker.Id, first.Id, AskBackIntegrationTests.Send("And?")));
        Assert.Equal(ApiErrorCodes.AskBackLimitReached, limit.ErrorCode);
        await service.SendMessageAsync(scene.Asker.Id, second.Id, AskBackIntegrationTests.Send("Why that?"));

        // Granting it again changes nothing.
        await entitlements.GrantCardUnlockAsync(scene.Asker.Id, "poll", scene.Poll.Id);
        Assert.Equal(1, await context.CardUnlocks.CountAsync(item => item.OwnerUserId == scene.Asker.Id));
    }

    [SkippableFact]
    public async Task CardUnlock_CanOnlyBeGrantedForTheOwnersCard()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await AskBackIntegrationTests.SceneAsync(context);
        var stranger = await AskBackIntegrationTests.AddUserAsync(context);

        var error = await Assert.ThrowsAsync<AppException>(
            () => AskBackIntegrationTests.EntitlementsFor(context).GrantCardUnlockAsync(stranger.Id, "poll", scene.Poll.Id));

        Assert.Equal(404, error.StatusCode);
        Assert.Equal(0, await context.CardUnlocks.CountAsync(item => item.OwnerUserId == stranger.Id));
    }

    [SkippableFact]
    public async Task Lite_UnlocksThreeCards_WithThreeQuestionsEach_AndRunsOutOfTickets()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var asker = await AskBackIntegrationTests.AddUserAsync(context);
        await AskBackIntegrationTests.ActivatePlanAsync(context, asker.Id, Plan.YakkuLiteCode);
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);
        var service = AskBackIntegrationTests.ServiceFor(context);

        var cards = new List<(Poll Poll, PollVote Vote, User Voter)>();
        for (var i = 0; i < 4; i++)
        {
            cards.Add(await CardWithReasonAsync(context, asker.Id));
        }

        var before = await entitlements.GetAccessAsync(asker.Id, cards[0].Vote.Id);
        Assert.False(before.Unlocked);
        Assert.Equal(3, before.TicketsLeft);
        Assert.True(before.CanUnlock);
        Assert.Equal("yakku_lite", before.PlanCode);
        Assert.NotNull(before.PlanExpiresAt);

        // The owner picks which cards to unlock; unlocking one again costs nothing.
        var unlocked = await entitlements.UnlockAsync(asker.Id, cards[0].Vote.Id);
        Assert.True(unlocked.Unlocked);
        Assert.Equal("plan", unlocked.UnlockSource);
        Assert.Equal(2, unlocked.TicketsLeft);
        Assert.Equal(2, (await entitlements.UnlockAsync(asker.Id, cards[0].Vote.Id)).TicketsLeft);
        await entitlements.UnlockAsync(asker.Id, cards[1].Vote.Id);
        Assert.Equal(0, (await entitlements.UnlockAsync(asker.Id, cards[2].Vote.Id)).TicketsLeft);

        var none = await Assert.ThrowsAsync<AppException>(() => entitlements.UnlockAsync(asker.Id, cards[3].Vote.Id));
        Assert.Equal(403, none.StatusCode);
        Assert.Equal(ApiErrorCodes.AskBackUnlockRequired, none.ErrorCode);

        // Three questions per reason on an unlocked card, each answered in turn.
        var (conversation, _) = await service.CreateConversationAsync(
            asker.Id, new CreateAskBackConversationRequest { VoteId = cards[0].Vote.Id });
        Assert.Equal(3, conversation.MessagesLeft);
        for (var round = 1; round <= 3; round++)
        {
            await service.SendMessageAsync(asker.Id, conversation.Id, AskBackIntegrationTests.Send($"Question {round}"));
            Assert.Equal(3 - round, (await service.GetConversationAsync(asker.Id, conversation.Id)).MessagesLeft);
            await service.SendMessageAsync(cards[0].Voter.Id, conversation.Id, AskBackIntegrationTests.Send($"Answer {round}"));
        }

        var done = await service.GetConversationAsync(asker.Id, conversation.Id);
        Assert.Equal("done", done.Turn);
        var extra = await Assert.ThrowsAsync<AppException>(() => service.SendMessageAsync(
            asker.Id, conversation.Id, AskBackIntegrationTests.Send("Question 4")));
        Assert.Equal(ApiErrorCodes.AskBackLimitReached, extra.ErrorCode);

        // A card without a ticket stays locked: a plan gives questions only on the cards it unlocked.
        var locked = await Assert.ThrowsAsync<AppException>(() => service.CreateConversationAsync(
            asker.Id, new CreateAskBackConversationRequest { VoteId = cards[3].Vote.Id }));
        Assert.Equal(ApiErrorCodes.AskBackLocked, locked.ErrorCode);
    }

    [SkippableFact]
    public async Task Lite_BuyingAgain_RestartsThePeriod_AndGivesNewTickets()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var asker = await AskBackIntegrationTests.AddUserAsync(context);
        await AskBackIntegrationTests.ActivatePlanAsync(context, asker.Id, Plan.YakkuLiteCode);
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);
        var cards = new List<(Poll Poll, PollVote Vote, User Voter)>();
        for (var i = 0; i < 4; i++)
        {
            cards.Add(await CardWithReasonAsync(context, asker.Id));
        }

        for (var i = 0; i < 3; i++)
        {
            await entitlements.UnlockAsync(asker.Id, cards[i].Vote.Id);
        }

        await Assert.ThrowsAsync<AppException>(() => entitlements.UnlockAsync(asker.Id, cards[3].Vote.Id));

        // Pretend the first activation began a while ago, then buy the same plan again.
        await context.Users.Where(u => u.Id == asker.Id).ExecuteUpdateAsync(set => set
            .SetProperty(u => u.PlanActivatedAt, DateTime.UtcNow.AddDays(-3))
            .SetProperty(u => u.PlanExpiresAt, DateTime.UtcNow.AddDays(4)));
        context.ChangeTracker.Clear();
        var restarted = await entitlements.ActivatePlanAsync(asker.Id, Plan.YakkuLiteCode);

        Assert.True(restarted.ExpiresAt > DateTime.UtcNow.AddDays(6.5));
        var fourth = await entitlements.UnlockAsync(asker.Id, cards[3].Vote.Id);
        Assert.Equal(2, fourth.TicketsLeft);
    }

    [SkippableFact]
    public async Task Love_AutoUnlocks_OnTheFirstAskBack_AndHasNoQuestionLimit()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await AskBackIntegrationTests.StartedAsync(context);
        var service = AskBackIntegrationTests.ServiceFor(context);

        var unlock = await context.CardUnlocks.AsNoTracking().SingleAsync(item => item.OwnerUserId == scene.Asker.Id);
        Assert.Equal(CardUnlockSource.Plan, unlock.Source);
        Assert.Equal(scene.Poll.Id, unlock.ContentId);

        for (var round = 1; round <= 5; round++)
        {
            await service.SendMessageAsync(scene.Asker.Id, conversationId, AskBackIntegrationTests.Send($"Question {round}"));
            await service.SendMessageAsync(scene.Voter.Id, conversationId, AskBackIntegrationTests.Send($"Answer {round}"));
        }

        var details = await service.GetConversationAsync(scene.Asker.Id, conversationId);
        Assert.Equal("me", details.Turn);
        Assert.Null(details.MessagesLeft);
        Assert.True(details.CanSend);
        Assert.Equal(10, await context.AskBackMessages.CountAsync(m => m.ConversationId == conversationId));
    }

    // ---- reading reasons ----

    [SkippableFact]
    public async Task Free_ShowsOnlyTheTopReason_AndNeverSendsTheTextOfTheRest()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AskBackIntegrationTests.AddUserAsync(context);
        var (poll, optionA, newest) = await ThreeReasonsAsync(context, owner.Id);

        var reasons = await new ReasonRepository(context).GetPollOptionReasonsAsync(poll.Id, owner.Id, optionA);

        Assert.Equal(3, reasons!.Items.Count);
        Assert.Equal(2, reasons.HiddenCount);
        var top = Assert.Single(reasons.Items, item => !item.IsHidden);
        Assert.Equal(newest, top.Id);
        Assert.Equal("The newest reason", top.Text);
        Assert.All(reasons.Items.Where(item => item.IsHidden), item => Assert.Equal(string.Empty, item.Text));
        Assert.DoesNotContain(reasons.Items, item => item.Text.Contains("older", StringComparison.Ordinal));

        var access = await AskBackIntegrationTests.EntitlementsFor(context)
            .GetOwnedCardAccessAsync(owner.Id, "poll", poll.Id);
        Assert.False(access.ReasonsRevealed);
        Assert.Equal("free", access.PlanCode);
    }

    [SkippableTheory]
    [InlineData(null)]
    [InlineData(Plan.YakkuLiteCode)]
    [InlineData(Plan.YakkuLoveCode)]
    public async Task CustomAnswers_FollowTheSameRule_OneOpenOnFree_AllOpenOnAPaidPlan(string? planCode)
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AskBackIntegrationTests.AddUserAsync(context);
        if (planCode is not null)
        {
            await AskBackIntegrationTests.ActivatePlanAsync(context, owner.Id, planCode);
        }

        // Oldest to newest: no reason, a reason, a reason.
        var poll = await AskBackIntegrationTests.AddPollAsync(context, owner.Id);
        var plain = await AddCustomAnswerAsync(context, poll, "Plain answer", reason: null);
        var second = await AddCustomAnswerAsync(context, poll, "Second answer", "Because two");
        var third = await AddCustomAnswerAsync(context, poll, "Third answer", "Because three");

        var list = await new ReasonRepository(context).GetPollCustomAnswersAsync(poll.Id, owner.Id);

        Assert.Equal(3, list!.Items.Count);
        if (planCode is not null)
        {
            Assert.Equal(0, list.HiddenCount);
            Assert.All(list.Items, item => Assert.False(item.IsHidden));
            Assert.Contains(list.Items, item => item.Answer == "Plain answer" && item.Reason is null);
            Assert.Contains(list.Items, item => item.Answer == "Second answer" && item.Reason == "Because two");
            return;
        }

        // Free: the newest answer that has a reason stays open; the others lose answer and reason.
        Assert.Equal(2, list.HiddenCount);
        var open = Assert.Single(list.Items, item => !item.IsHidden);
        Assert.Equal(third, open.Id);
        Assert.Equal("Third answer", open.Answer);
        Assert.Equal("Because three", open.Reason);
        foreach (var hidden in list.Items.Where(item => item.IsHidden))
        {
            Assert.Equal(string.Empty, hidden.Answer);
            Assert.Null(hidden.Reason);
            Assert.Contains(hidden.Id, new[] { plain, second });
        }
    }

    [SkippableFact]
    public async Task CustomAnswers_OpenWhenTheCardIsBought()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AskBackIntegrationTests.AddUserAsync(context);
        var poll = await AskBackIntegrationTests.AddPollAsync(context, owner.Id);
        await AddCustomAnswerAsync(context, poll, "One", "Because one");
        await AddCustomAnswerAsync(context, poll, "Two", "Because two");
        await AddCustomAnswerAsync(context, poll, "Three", "Because three");

        var before = await new ReasonRepository(context).GetPollCustomAnswersAsync(poll.Id, owner.Id);
        Assert.Equal(2, before!.HiddenCount);

        await AskBackIntegrationTests.EntitlementsFor(context).GrantCardUnlockAsync(owner.Id, "poll", poll.Id);

        var after = await new ReasonRepository(context).GetPollCustomAnswersAsync(poll.Id, owner.Id);
        Assert.Equal(0, after!.HiddenCount);
        Assert.Contains(after.Items, item => item.Answer == "One" && item.Reason == "Because one");
    }

    [SkippableTheory]
    [InlineData(Plan.YakkuLiteCode)]
    [InlineData(Plan.YakkuLoveCode)]
    public async Task APaidPlan_RevealsEveryReason(string planCode)
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AskBackIntegrationTests.AddUserAsync(context);
        await AskBackIntegrationTests.ActivatePlanAsync(context, owner.Id, planCode);
        var (poll, optionA, _) = await ThreeReasonsAsync(context, owner.Id);

        var reasons = await new ReasonRepository(context).GetPollOptionReasonsAsync(poll.Id, owner.Id, optionA);

        Assert.Equal(0, reasons!.HiddenCount);
        Assert.All(reasons.Items, item => Assert.False(item.IsHidden));
        Assert.DoesNotContain(reasons.Items, item => item.Text.Length == 0);
        Assert.True((await AskBackIntegrationTests.EntitlementsFor(context)
            .GetOwnedCardAccessAsync(owner.Id, "poll", poll.Id)).ReasonsRevealed);
    }

    [SkippableFact]
    public async Task BuyingTheCard_RevealsItsReasons_ButNotTheOnesOnAnotherCard()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AskBackIntegrationTests.AddUserAsync(context);
        var (bought, boughtOption, _) = await ThreeReasonsAsync(context, owner.Id);
        var (other, otherOption, _) = await ThreeReasonsAsync(context, owner.Id);
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);
        await entitlements.GrantCardUnlockAsync(owner.Id, "poll", bought.Id);

        var repository = new ReasonRepository(context);
        Assert.Equal(0, (await repository.GetPollOptionReasonsAsync(bought.Id, owner.Id, boughtOption))!.HiddenCount);
        Assert.Equal(2, (await repository.GetPollOptionReasonsAsync(other.Id, owner.Id, otherOption))!.HiddenCount);
        Assert.True((await entitlements.GetOwnedCardAccessAsync(owner.Id, "poll", bought.Id)).ReasonsRevealed);
        Assert.False((await entitlements.GetOwnedCardAccessAsync(owner.Id, "poll", other.Id)).ReasonsRevealed);
    }

    [SkippableFact]
    public async Task WhenThePlanEnds_ReasonsAreHiddenAgain_ButABoughtCardStaysOpen()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AskBackIntegrationTests.AddUserAsync(context);
        await AskBackIntegrationTests.ActivatePlanAsync(context, owner.Id, Plan.YakkuLiteCode);
        var (viaPlan, planOption, _) = await ThreeReasonsAsync(context, owner.Id);
        var (bought, boughtOption, _) = await ThreeReasonsAsync(context, owner.Id);
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);
        await entitlements.GrantCardUnlockAsync(owner.Id, "poll", bought.Id);

        await AskBackIntegrationTests.ExpirePaidPlanAsync(context, owner.Id);

        var repository = new ReasonRepository(context);
        Assert.Equal(2, (await repository.GetPollOptionReasonsAsync(viaPlan.Id, owner.Id, planOption))!.HiddenCount);
        Assert.Equal(0, (await repository.GetPollOptionReasonsAsync(bought.Id, owner.Id, boughtOption))!.HiddenCount);
        Assert.False((await entitlements.GetOwnedCardAccessAsync(owner.Id, "poll", viaPlan.Id)).ReasonsRevealed);
    }

    [SkippableFact]
    public async Task CardAccess_IsOnlyForTheOwner_AndNeedsAKnownContentType()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AskBackIntegrationTests.AddUserAsync(context);
        var stranger = await AskBackIntegrationTests.AddUserAsync(context);
        var (poll, _, _) = await ThreeReasonsAsync(context, owner.Id);
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);

        var notOwner = await Assert.ThrowsAsync<AppException>(
            () => entitlements.GetOwnedCardAccessAsync(stranger.Id, "poll", poll.Id));
        Assert.Equal(404, notOwner.StatusCode);
        var badType = await Assert.ThrowsAsync<AppException>(
            () => entitlements.GetOwnedCardAccessAsync(owner.Id, "survey", poll.Id));
        Assert.Equal(400, badType.StatusCode);
    }

    // ---- plan expiry ----

    [SkippableFact]
    public async Task ExpiredPlan_PausesPlanUnlockedChats_ButNotPurchasedOrFreeOnes_AndBuyingAgainResumes()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var asker = await AskBackIntegrationTests.AddUserAsync(context);
        await AskBackIntegrationTests.ActivatePlanAsync(context, asker.Id, Plan.YakkuLiteCode);
        var entitlements = AskBackIntegrationTests.EntitlementsFor(context);
        var service = AskBackIntegrationTests.ServiceFor(context);

        var planCard = await CardWithReasonAsync(context, asker.Id);
        var boughtCard = await CardWithReasonAsync(context, asker.Id);
        var otherBoughtCard = await CardWithReasonAsync(context, asker.Id);
        var laterCard = await CardWithReasonAsync(context, asker.Id);

        await entitlements.UnlockAsync(asker.Id, planCard.Vote.Id);
        await entitlements.GrantCardUnlockAsync(asker.Id, "poll", boughtCard.Poll.Id);
        await entitlements.GrantCardUnlockAsync(asker.Id, "poll", otherBoughtCard.Poll.Id);
        var (plan, _) = await service.CreateConversationAsync(
            asker.Id, new CreateAskBackConversationRequest { VoteId = planCard.Vote.Id });
        var (bought, _) = await service.CreateConversationAsync(
            asker.Id, new CreateAskBackConversationRequest { VoteId = boughtCard.Vote.Id });
        var (other, _) = await service.CreateConversationAsync(
            asker.Id, new CreateAskBackConversationRequest { VoteId = otherBoughtCard.Vote.Id });
        Assert.Equal(3, plan.MessagesLeft);

        // The plan runs out.
        await AskBackIntegrationTests.ExpirePaidPlanAsync(context, asker.Id);
        context.ChangeTracker.Clear();

        var paused = await service.GetConversationAsync(asker.Id, plan.Id);
        Assert.True(paused.IsPaused);
        Assert.False(paused.CanSend);
        var pausedForVoter = await service.GetConversationAsync(planCard.Voter.Id, plan.Id);
        Assert.True(pausedForVoter.IsPaused);
        Assert.False(pausedForVoter.CanSend);
        foreach (var user in new[] { asker.Id, planCard.Voter.Id })
        {
            var error = await Assert.ThrowsAsync<AppException>(() => service.SendMessageAsync(
                user, plan.Id, AskBackIntegrationTests.Send("hello")));
            Assert.Equal(403, error.StatusCode);
            Assert.Equal(ApiErrorCodes.AskBackPlanExpired, error.ErrorCode);
        }

        var access = await entitlements.GetAccessAsync(asker.Id, planCard.Vote.Id);
        Assert.True(access.PlanExpired);
        Assert.True(access.Paused);

        // Single-card unlocks keep working.
        var stillBought = await service.GetConversationAsync(asker.Id, bought.Id);
        Assert.False(stillBought.IsPaused);
        await service.SendMessageAsync(asker.Id, bought.Id, AskBackIntegrationTests.Send("Still here"));
        var stillOther = await service.GetConversationAsync(asker.Id, other.Id);
        Assert.False(stillOther.IsPaused);
        await service.SendMessageAsync(asker.Id, other.Id, AskBackIntegrationTests.Send("Me too"));

        // An expired plan has no tickets left to spend, and the plan card cannot start another chat.
        var noTicket = await Assert.ThrowsAsync<AppException>(
            () => entitlements.UnlockAsync(asker.Id, laterCard.Vote.Id));
        Assert.Equal(ApiErrorCodes.AskBackUnlockRequired, noTicket.ErrorCode);

        // Buying a plan again brings the paused chat back.
        await AskBackIntegrationTests.ActivatePlanAsync(context, asker.Id, Plan.YakkuLiteCode);
        var resumed = await service.GetConversationAsync(asker.Id, plan.Id);
        Assert.False(resumed.IsPaused);
        Assert.True(resumed.CanSend);
        await service.SendMessageAsync(asker.Id, plan.Id, AskBackIntegrationTests.Send("Welcome back"));
    }

    // ---- block ----

    [SkippableFact]
    public async Task Block_StopsNewConversationsInBothDirections_AndNeverTellsTheBlockedPerson()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await AskBackIntegrationTests.StartedAsync(context);
        var service = AskBackIntegrationTests.ServiceFor(context);

        // The voter owns a card too, and the asker answered it.
        var voterCard = await CardWithReasonAsync(context, scene.Voter.Id, scene.Asker.Id);
        // The asker owns another card the voter answered.
        var askerCard = await CardWithReasonAsync(context, scene.Asker.Id, scene.Voter.Id);

        await service.BlockAsync(scene.Voter.Id, conversationId, new BlockAskBackRequest { Reason = "Harassing me" });

        // Neither can open a new chat with the other.
        var byAsker = await Assert.ThrowsAsync<AppException>(() => service.CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = askerCard.Vote.Id }));
        var byVoter = await Assert.ThrowsAsync<AppException>(() => service.CreateConversationAsync(
            scene.Voter.Id, new CreateAskBackConversationRequest { VoteId = voterCard.Vote.Id }));
        Assert.Equal(403, byAsker.StatusCode);
        Assert.Equal(403, byVoter.StatusCode);

        // The blocked person is not told: the blocker's chat just reads as closed to them.
        var blocked = await service.GetConversationAsync(scene.Asker.Id, conversationId);
        Assert.False(blocked.CanUnblock);
    }

    [SkippableFact]
    public async Task Block_ClosesTheBlockersPollsAndChecksToTheBlockedPerson()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await AskBackIntegrationTests.StartedAsync(context);
        var checks = new CheckService(new CheckRepository(context), new CastCheckVoteValidator(), new NoOpStreakService());
        var liveCheck = (await checks.GetChecksAsync(scene.Voter.Id)).First();
        var madeCheck = await checks.MakeAsync(
            scene.Voter.Id,
            new MakeCheckRequest { CheckId = liveCheck.Id, CheckOptionId = liveCheck.Options[0].Id });
        var blockerPoll = await AskBackIntegrationTests.AddPollAsync(context, scene.Voter.Id);
        var otherPoll = await AskBackIntegrationTests.AddPollAsync(context, scene.Voter.Id);
        var bystander = await AskBackIntegrationTests.AddUserAsync(context);
        var blocks = new UserBlockRepository(context);
        var repository = new VotableContentRepository(context);
        var shared = new SharedContentService(repository, new FakeObjectStorage(), MediaUploadPolicy.Default, blocks);
        var voting = new VotingService(
            repository,
            new FakeObjectStorage(),
            MediaUploadPolicy.Default,
            new VoteRequestValidator(),
            new RecordingAuditLogWriter(),
            new NoOpStreakService(),
            new RecordingVoteNotificationOutbox(),
            blocks);

        var beforeBlock = await shared.GetAsync(blockerPoll.ShareCode, VoterIdentity.ForUser(scene.Asker.Id));
        Assert.True(beforeBlock.Voting.CanVote);
        Assert.True((await shared.GetAsync(madeCheck.ShareCode, VoterIdentity.ForUser(scene.Asker.Id))).Voting.CanVote);

        await AskBackIntegrationTests.ServiceFor(context)
            .BlockAsync(scene.Voter.Id, conversationId, new BlockAskBackRequest { Reason = "Spam or scam" });

        var checkAsBlocked = await shared.GetAsync(madeCheck.ShareCode, VoterIdentity.ForUser(scene.Asker.Id));
        Assert.False(checkAsBlocked.Voting.CanVote);
        Assert.Equal("INACTIVE", checkAsBlocked.Voting.BlockedReason);

        // To the blocked person it is a closed poll. Everyone else still sees it open.
        var asBlocked = await shared.GetAsync(blockerPoll.ShareCode, VoterIdentity.ForUser(scene.Asker.Id));
        Assert.False(asBlocked.Voting.CanVote);
        Assert.Equal("CLOSED", asBlocked.Voting.BlockedReason);
        Assert.True((await shared.GetAsync(blockerPoll.ShareCode, VoterIdentity.ForUser(bystander.Id))).Voting.CanVote);

        var rejected = await Assert.ThrowsAsync<AppException>(() => voting.VoteAsync(
            new VoteRequest { ShareCode = otherPoll.ShareCode, OptionId = otherPoll.Options.First().Id },
            VoterIdentity.ForUser(scene.Asker.Id)));
        Assert.Equal(400, rejected.StatusCode);
        Assert.Contains("not accepting votes", rejected.Message);
        Assert.False(await context.PollVotes.AnyAsync(v => v.PollId == otherPoll.Id && v.VoterId == scene.Asker.Id));

        // The block is one way: the blocker can still answer the asker's poll.
        var askerPoll = await AskBackIntegrationTests.AddPollAsync(context, scene.Asker.Id);
        var asBlocker = await shared.GetAsync(askerPoll.ShareCode, VoterIdentity.ForUser(scene.Voter.Id));
        Assert.True(asBlocker.Voting.CanVote);

        // Lifting the block opens the polls again.
        await AskBackIntegrationTests.ServiceFor(context).UnblockAsync(scene.Voter.Id, conversationId);
        Assert.True((await shared.GetAsync(blockerPoll.ShareCode, VoterIdentity.ForUser(scene.Asker.Id))).Voting.CanVote);
    }

    // ---- helpers ----

    private static async Task<(PollVote Vote, User Voter)> AddVoteAsync(
        YakkuDbContext context,
        Poll poll,
        Guid optionId,
        string? reason,
        Guid? voterId = null)
    {
        var voter = voterId is null ? await AskBackIntegrationTests.AddUserAsync(context) : null;
        var vote = PollVote.ForOption(voterId ?? voter!.Id, poll.Id, optionId, reason);
        context.PollVotes.Add(vote);
        await context.SaveChangesAsync();
        return (vote, voter ?? await context.Users.FirstAsync(user => user.Id == voterId));
    }

    /// <summary>A registered user writes their own answer ("Something else") on a poll, with a reason if given.</summary>
    private static async Task<Guid> AddCustomAnswerAsync(YakkuDbContext context, Poll poll, string answer, string? reason)
    {
        var voter = await AskBackIntegrationTests.AddUserAsync(context);
        var vote = PollVote.ForCustomAnswer(voter.Id, poll.Id, answer, reason);
        context.PollVotes.Add(vote);
        await context.SaveChangesAsync();
        return vote.Id;
    }

    /// <summary>
    /// A poll with three reasons on its first option, oldest first, from three registered users: enough votes to
    /// reveal reasons. Returns the poll, that option and the id of the newest reason (the option's top reason).
    /// </summary>
    private static async Task<(Poll Poll, Guid OptionId, Guid NewestVoteId)> ThreeReasonsAsync(
        YakkuDbContext context,
        Guid ownerId)
    {
        var poll = await AskBackIntegrationTests.AddPollAsync(context, ownerId);
        var option = poll.Options.First().Id;
        await AddVoteAsync(context, poll, option, "The oldest reason");
        await AddVoteAsync(context, poll, option, "An older reason");
        var newest = await AddVoteAsync(context, poll, option, "The newest reason");
        return (poll, option, newest.Vote.Id);
    }

    /// <summary>A poll owned by <paramref name="ownerId"/> with enough votes to reveal reasons, one with a reason.</summary>
    internal static async Task<(Poll Poll, PollVote Vote, User Voter)> CardWithReasonAsync(
        YakkuDbContext context,
        Guid ownerId,
        Guid? voterId = null)
    {
        var poll = await AskBackIntegrationTests.AddPollAsync(context, ownerId);
        var (vote, voter) = await AddVoteAsync(
            context, poll, poll.Options.First().Id, "Because it is clearer", voterId);
        for (var i = 0; i < 2; i++)
        {
            await AddVoteAsync(context, poll, poll.Options.Last().Id, reason: null);
        }

        return (poll, vote, voter);
    }
}
