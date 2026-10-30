using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.Application.AskBack;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.AskBack.Interfaces;
using Yakku.Application.AskBack.Services;
using Yakku.Application.AskBack.Validators;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.PushNotifications.DTOs;
using Yakku.Application.PushNotifications.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;
using Yakku.Infrastructure.Push;

namespace Yakku.Tests;

/// <summary>Real-PostgreSQL checks for Ask Back: rules, authorization, constraints, concurrency and paging.</summary>
[Collection(PostgresCollection.Name)]
public sealed class AskBackIntegrationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public AskBackIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    // ---- creating a conversation ----

    [SkippableFact]
    public async Task Creator_CanStartAConversation_ForAnEligibleVote_AndSeesOnlyAnonymousIdentities()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);

        var (conversation, created) = await ServiceFor(context)
            .CreateConversationAsync(scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id });

        Assert.True(created);
        Assert.Equal("active", conversation.Status);
        Assert.Equal("asker", conversation.MyRole);
        Assert.Equal(scene.Voter.Profile.AnonymousName, conversation.Other.AnonymousName);
        Assert.Equal("Which presentation is better?", conversation.Context.Question);
        Assert.Equal("Option A", conversation.Context.SelectedOption);
        Assert.Equal("Because it is clearer", conversation.Context.Reason);
        Assert.True(conversation.CanSend);

        // Nothing in the response can identify anyone.
        var json = JsonSerializer.Serialize(conversation);
        Assert.DoesNotContain(scene.Voter.PhoneNumber, json);
        Assert.DoesNotContain(scene.Asker.PhoneNumber, json);
        Assert.DoesNotContain(scene.Voter.Id.ToString(), json);
        Assert.DoesNotContain(scene.Asker.Id.ToString(), json);

        // Both participants exist, in the same transaction as the conversation.
        Assert.Equal(2, await context.AskBackParticipants.CountAsync(p => p.ConversationId == conversation.Id));
    }

    [SkippableFact]
    public async Task VoterSide_SeesTheAskerAnonymously()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var service = ServiceFor(context);
        var (conversation, _) = await service.CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id });

        var asVoter = await service.GetConversationAsync(scene.Voter.Id, conversation.Id);

        Assert.Equal("voter", asVoter.MyRole);
        Assert.Equal(scene.Asker.Profile.AnonymousName, asVoter.Other.AnonymousName);
        Assert.DoesNotContain(scene.Asker.PhoneNumber, JsonSerializer.Serialize(asVoter));
    }

    [SkippableFact]
    public async Task NonCreator_CannotStartAConversation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var stranger = await AddUserAsync(context);

        var asStranger = await Assert.ThrowsAsync<AppException>(() => ServiceFor(context)
            .CreateConversationAsync(stranger.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id }));
        var asVoter = await Assert.ThrowsAsync<AppException>(() => ServiceFor(context)
            .CreateConversationAsync(scene.Voter.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id }));

        Assert.Equal(404, asStranger.StatusCode);
        Assert.Equal(404, asVoter.StatusCode);
        Assert.Equal(0, await context.AskBackConversations.CountAsync(c => c.VoteId == scene.Vote.Id));
    }

    [SkippableFact]
    public async Task UnknownVote_IsNotFound()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var asker = await AddUserAsync(context);

        var error = await Assert.ThrowsAsync<AppException>(() => ServiceFor(context)
            .CreateConversationAsync(asker.Id, new CreateAskBackConversationRequest { VoteId = Guid.NewGuid() }));

        Assert.Equal(404, error.StatusCode);
    }

    [SkippableFact]
    public async Task OwnVote_CannotStartAConversation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var ownVote = PollVote.ForOption(
            scene.Asker.Id, scene.Poll.Id, scene.Poll.Options.First().Id, "my own reason");
        context.PollVotes.Add(ownVote);
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AppException>(() => ServiceFor(context)
            .CreateConversationAsync(scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = ownVote.Id }));

        Assert.Equal(400, error.StatusCode);
        Assert.Equal(0, await context.AskBackConversations.CountAsync(c => c.VoteId == ownVote.Id));
    }

    [SkippableFact]
    public async Task VoteWithoutAReason_CannotStartAConversation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var other = await AddUserAsync(context);
        var noReason = PollVote.ForOption(other.Id, scene.Poll.Id, scene.Poll.Options.First().Id, reason: null);
        context.PollVotes.Add(noReason);
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AppException>(() => ServiceFor(context)
            .CreateConversationAsync(scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = noReason.Id }));

        Assert.Equal(400, error.StatusCode);
    }

    [SkippableFact]
    public async Task GuestVote_CannotStartAConversation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var guestVote = PollVote.ForOption(
            Guid.NewGuid(), scene.Poll.Id, scene.Poll.Options.First().Id, "guest reason", VoterRole.Guest);
        context.PollVotes.Add(guestVote);
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AppException>(() => ServiceFor(context)
            .CreateConversationAsync(scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = guestVote.Id }));

        Assert.Equal(403, error.StatusCode);
        Assert.Equal(0, await context.AskBackConversations.CountAsync(c => c.VoteId == guestVote.Id));
    }

    [SkippableFact]
    public async Task VoterPendingDeletion_CannotBeAskedBack()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        scene.Voter.RequestDeletion(DateTime.UtcNow, TimeSpan.FromDays(30));
        await context.SaveChangesAsync();

        var error = await Assert.ThrowsAsync<AppException>(() => ServiceFor(context)
            .CreateConversationAsync(scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id }));

        Assert.Equal(403, error.StatusCode);
    }

    [SkippableFact]
    public async Task AskingAgain_ReturnsTheExistingConversation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await SceneAsync(context);
        var service = ServiceFor(context);
        var request = new CreateAskBackConversationRequest { VoteId = scene.Vote.Id };

        var (first, firstCreated) = await service.CreateConversationAsync(scene.Asker.Id, request);
        var (second, secondCreated) = await service.CreateConversationAsync(scene.Asker.Id, request);

        Assert.True(firstCreated);
        Assert.False(secondCreated);
        Assert.Equal(first.Id, second.Id);
        Assert.Equal(1, await context.AskBackConversations.CountAsync(c => c.VoteId == scene.Vote.Id));
    }

    [SkippableFact]
    public async Task ConcurrentCreation_ForOneVote_MakesExactlyOneConversation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var scene = await SceneAsync(setup);

        var results = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ =>
        {
            await using var context = _fixture.CreateContext();
            return await ServiceFor(context).CreateConversationAsync(
                scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id });
        }));

        Assert.Single(results.Select(r => r.Conversation.Id).Distinct());
        Assert.Equal(1, results.Count(r => r.Created));
        Assert.Equal(1, await setup.AskBackConversations.CountAsync(c => c.VoteId == scene.Vote.Id));
        Assert.Equal(2, await setup.AskBackParticipants.CountAsync(
            p => p.ConversationId == results[0].Conversation.Id));
    }

    [SkippableFact]
    public async Task MadeCheck_ConversationsWorkToo()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var voter = await AddUserAsync(context);
        var check = await context.Checks.Include(c => c.Options).FirstAsync();
        var userCheck = UserCheck.Create(owner.Id, check.Id, $"chk{Interlocked.Increment(ref _sequence)}abcdef");
        context.UserChecks.Add(userCheck);
        var vote = UserCheckVote.ForOption(userCheck.Id, check.Id, check.Options[0].Id, voter.Id, "check reason");
        context.UserCheckVotes.Add(vote);
        await context.SaveChangesAsync();

        var (conversation, created) = await ServiceFor(context)
            .CreateConversationAsync(owner.Id, new CreateAskBackConversationRequest { VoteId = vote.Id });

        Assert.True(created);
        Assert.Equal("check", conversation.Context.ContentType);
        Assert.Equal(check.Question, conversation.Context.Question);
        Assert.Equal(check.Options[0].Text, conversation.Context.SelectedOption);
        Assert.Equal("check reason", conversation.Context.Reason);
    }

    // ---- who can see a conversation ----

    [SkippableFact]
    public async Task OnlyParticipants_CanReadSendOrJoin()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);
        var outsider = await AddUserAsync(context);
        // Another voter on the same poll is still an outsider to this conversation.
        var fellowVoter = await AddUserAsync(context);

        foreach (var user in new[] { outsider, fellowVoter })
        {
            Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(
                () => service.GetConversationAsync(user.Id, conversationId))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(
                () => service.ListMessagesAsync(user.Id, conversationId, null, null, null))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(
                () => service.SendMessageAsync(user.Id, conversationId, Send("hello")))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(
                () => service.MarkReadAsync(user.Id, conversationId, new MarkAskBackReadRequest { LastReadMessageId = Guid.NewGuid() }))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(
                () => service.CloseAsync(user.Id, conversationId))).StatusCode);
            Assert.Equal(404, (await Assert.ThrowsAsync<AppException>(
                () => service.BlockAsync(user.Id, conversationId, new BlockAskBackRequest()))).StatusCode);
            Assert.False(await service.CanJoinAsync(user.Id, conversationId));
            Assert.Empty((await service.ListConversationsAsync(user.Id, null, null)).Items);
        }

        Assert.True(await service.CanJoinAsync(scene.Asker.Id, conversationId));
        Assert.True(await service.CanJoinAsync(scene.Voter.Id, conversationId));
        Assert.Equal(0, await context.AskBackMessages.CountAsync(m => m.ConversationId == conversationId));
    }

    // ---- messages ----

    [SkippableFact]
    public async Task SendingTheSameClientMessageIdTwice_StoresOneMessage()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);
        var request = Send("Could you tell me more?");

        var first = await service.SendMessageAsync(scene.Asker.Id, conversationId, request);
        var retry = await service.SendMessageAsync(scene.Asker.Id, conversationId, request);

        Assert.Equal(first.Id, retry.Id);
        Assert.Equal(1, await context.AskBackMessages.CountAsync(m => m.ConversationId == conversationId));
        Assert.Equal(1, await context.AskBackOutbox.CountAsync(o => o.ConversationId == conversationId));
    }

    [SkippableFact]
    public async Task ConcurrentRetries_OfOneMessage_StoreOneMessage()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(setup);
        var request = Send("Racing retries");

        var results = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var context = _fixture.CreateContext();
            return await ServiceFor(context).SendMessageAsync(scene.Asker.Id, conversationId, request);
        }));

        Assert.Single(results.Select(r => r.Id).Distinct());
        Assert.Equal(1, await setup.AskBackMessages.CountAsync(m => m.ConversationId == conversationId));
    }

    [SkippableFact]
    public async Task Message_IsPersisted_WithServerTime_AndHidesTheSenderId()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);
        var before = DateTime.UtcNow.AddSeconds(-1);

        var sent = await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("  Why that one?  "));

        Assert.Equal("Why that one?", sent.Content);
        Assert.True(sent.IsMine);
        Assert.Equal(scene.Asker.Profile.AnonymousName, sent.SenderName);
        Assert.InRange(sent.CreatedAt, before, DateTime.UtcNow.AddSeconds(1));
        Assert.DoesNotContain(scene.Asker.Id.ToString(), JsonSerializer.Serialize(sent));

        // The other side sees the same message as not theirs, without the asker's identity.
        var asVoter = (await service.ListMessagesAsync(scene.Voter.Id, conversationId, null, null, null)).Items.Single();
        Assert.False(asVoter.IsMine);
        Assert.Equal(scene.Asker.Profile.AnonymousName, asVoter.SenderName);
        Assert.DoesNotContain(scene.Asker.PhoneNumber, JsonSerializer.Serialize(asVoter));

        // The original reason is not copied into the chat.
        Assert.DoesNotContain(
            await context.AskBackMessages.Where(m => m.ConversationId == conversationId).Select(m => m.Content).ToListAsync(),
            content => content.Contains("Because it is clearer"));
    }

    [SkippableTheory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyMessage_IsRejected(string content)
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);

        await Assert.ThrowsAsync<ValidationException>(() => ServiceFor(context)
            .SendMessageAsync(scene.Asker.Id, conversationId, Send(content)));
    }

    [SkippableFact]
    public async Task MessageLength_IsLimitedTo2000Characters()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);

        var ok = await service.SendMessageAsync(scene.Asker.Id, conversationId, Send(new string('a', 2000)));
        await Assert.ThrowsAsync<ValidationException>(() => service
            .SendMessageAsync(scene.Asker.Id, conversationId, Send(new string('a', 2001))));
        await Assert.ThrowsAsync<ValidationException>(() => service
            .SendMessageAsync(scene.Asker.Id, conversationId, new SendAskBackMessageRequest { Content = "x" }));

        Assert.Equal(2000, ok.Content.Length);
        Assert.Equal(1, await context.AskBackMessages.CountAsync(m => m.ConversationId == conversationId));
    }

    [SkippableFact]
    public async Task BothParticipants_CanSend_AndMessagesPersistForAnOfflineRecipient()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var realtime = new RecordingRealtime();
        var service = ServiceFor(context, realtime);

        await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("Hi"));
        await service.SendMessageAsync(scene.Voter.Id, conversationId, Send("Hello"));

        // Saved even though nobody is connected; a later fetch returns both, oldest first.
        var history = (await ServiceFor(context).ListMessagesAsync(scene.Voter.Id, conversationId, null, null, null)).Items;
        Assert.Equal(["Hi", "Hello"], history.Select(m => m.Content));
        Assert.Equal([false, true], history.Select(m => m.IsMine));

        // The real-time events carry isMine per side and were published after the save.
        Assert.Equal(2, realtime.Messages.Count);
        Assert.True(realtime.Messages[0].ForSender.IsMine);
        Assert.False(realtime.Messages[0].ForRecipient.IsMine);
        Assert.Equal(scene.Voter.Id, realtime.Messages[0].Recipient);
    }

    [SkippableFact]
    public async Task FailedRealtimeDelivery_DoesNotFailTheSend()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);

        var sent = await ServiceFor(context, new RecordingRealtime { Throw = true })
            .SendMessageAsync(scene.Asker.Id, conversationId, Send("Still saved"));

        Assert.Equal(1, await context.AskBackMessages.CountAsync(m => m.Id == sent.Id));
    }

    // ---- paging ----

    [SkippableFact]
    public async Task MessagePaging_HasNoDuplicatesOrGaps_EvenWithIdenticalTimestamps()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);

        // 12 messages, four sharing one timestamp, so the (created_at, id) tie-break is exercised.
        var baseTime = DateTime.UtcNow.AddMinutes(-30);
        var expected = new List<Guid>();
        for (var i = 0; i < 12; i++)
        {
            var at = i is >= 4 and < 8 ? baseTime.AddSeconds(4) : baseTime.AddSeconds(i);
            var sender = i % 2 == 0 ? scene.Asker.Id : scene.Voter.Id;
            var message = AskBackMessage.Create(conversationId, sender, Guid.NewGuid(), $"m{i}", at);
            context.AskBackMessages.Add(message);
        }

        await context.SaveChangesAsync();
        var ordered = await context.AskBackMessages
            .Where(m => m.ConversationId == conversationId)
            .OrderBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Select(m => m.Id).ToListAsync();
        expected.AddRange(ordered);

        // Walk backwards from the newest page, 5 at a time.
        var collected = new List<Guid>();
        string? cursor = null;
        var pages = 0;
        while (true)
        {
            var page = await service.ListMessagesAsync(scene.Asker.Id, conversationId, cursor, "older", 5);
            collected.InsertRange(0, page.Items.Select(m => m.Id));
            pages++;
            Assert.True(page.Items.Count <= 5);
            if (!page.HasMore)
            {
                Assert.Null(page.NextCursor);
                break;
            }

            cursor = page.NextCursor;
            Assert.NotNull(cursor);
        }

        Assert.Equal(3, pages);
        Assert.Equal(expected, collected);

        // Catch up forwards from the first message, 5 at a time.
        var forward = new List<Guid> { expected[0] };
        var firstCursor = AskBackCursor.Encode(
            (await context.AskBackMessages.AsNoTracking().FirstAsync(m => m.Id == expected[0])).CreatedAt, expected[0]);
        cursor = firstCursor;
        while (true)
        {
            var page = await service.ListMessagesAsync(scene.Voter.Id, conversationId, cursor, "newer", 5);
            forward.AddRange(page.Items.Select(m => m.Id));
            if (!page.HasMore)
            {
                break;
            }

            cursor = page.NextCursor;
        }

        Assert.Equal(expected, forward);
    }

    [SkippableFact]
    public async Task Messages_RejectABadCursorOrDirection()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);

        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(
            () => service.ListMessagesAsync(scene.Asker.Id, conversationId, "garbage", null, null))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(
            () => service.ListMessagesAsync(scene.Asker.Id, conversationId, null, "sideways", null))).StatusCode);
        Assert.Equal(400, (await Assert.ThrowsAsync<AppException>(
            () => service.ListMessagesAsync(scene.Asker.Id, conversationId, null, "newer", null))).StatusCode);
    }

    [SkippableFact]
    public async Task ConversationList_IsPaged_NewestActivityFirst_WithPreviewAndUnread()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var asker = await AddUserAsync(context);
        var poll = await AddPollAsync(context, asker.Id);
        var service = ServiceFor(context);
        var ids = new List<Guid>();
        var voters = new List<User>();
        for (var i = 0; i < 5; i++)
        {
            var voter = await AddUserAsync(context);
            voters.Add(voter);
            var vote = PollVote.ForOption(voter.Id, poll.Id, poll.Options.First().Id, $"reason {i}");
            context.PollVotes.Add(vote);
            await context.SaveChangesAsync();
            var (conversation, _) = await service.CreateConversationAsync(
                asker.Id, new CreateAskBackConversationRequest { VoteId = vote.Id });
            ids.Add(conversation.Id);
        }

        // The voter of the oldest conversation writes last, so it jumps to the top with an unread message.
        await service.SendMessageAsync(voters[0].Id, ids[0], Send(new string('x', 200)));

        var collected = new List<AskBackConversationListItemResponse>();
        string? cursor = null;
        while (true)
        {
            var page = await service.ListConversationsAsync(asker.Id, cursor, 2);
            collected.AddRange(page.Items);
            if (!page.HasMore)
            {
                break;
            }

            cursor = page.NextCursor;
        }

        Assert.Equal(5, collected.Count);
        Assert.Equal(5, collected.Select(c => c.Id).Distinct().Count());
        Assert.Equal(ids[0], collected[0].Id);
        Assert.Equal(1, collected[0].UnreadCount);
        Assert.False(collected[0].LastMessage!.IsMine);
        Assert.Equal(AskBackOptions.PreviewLength + 1, collected[0].LastMessage!.Preview.Length);
        Assert.Equal(voters[0].Profile.AnonymousName, collected[0].Other.AnonymousName);
        Assert.All(collected.Skip(1), c => Assert.Equal(0, c.UnreadCount));
    }

    // ---- read state ----

    [SkippableFact]
    public async Task ReadState_CountsUnreadPerParticipant_AndNeverMovesBackward()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);
        var realtime = new RecordingRealtime();
        var reading = ServiceFor(context, realtime);

        var m1 = await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("one"));
        var m2 = await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("two"));
        var m3 = await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("three"));
        await service.SendMessageAsync(scene.Voter.Id, conversationId, Send("reply"));

        Assert.Equal(3, (await service.GetConversationAsync(scene.Voter.Id, conversationId)).UnreadCount);
        Assert.Equal(1, (await service.GetConversationAsync(scene.Asker.Id, conversationId)).UnreadCount);

        var afterTwo = await reading.MarkReadAsync(
            scene.Voter.Id, conversationId, new MarkAskBackReadRequest { LastReadMessageId = m2.Id });
        Assert.True(afterTwo.Advanced);
        Assert.Equal(1, afterTwo.UnreadCount);
        Assert.Single(realtime.Reads);
        Assert.Equal(scene.Asker.Id, realtime.Reads[0].Other);

        // Going back to the first message is ignored and the count does not rise.
        var backward = await reading.MarkReadAsync(
            scene.Voter.Id, conversationId, new MarkAskBackReadRequest { LastReadMessageId = m1.Id });
        Assert.False(backward.Advanced);
        Assert.Equal(m2.Id, backward.LastReadMessageId);
        Assert.Equal(1, backward.UnreadCount);
        Assert.Single(realtime.Reads);

        var all = await reading.MarkReadAsync(
            scene.Voter.Id, conversationId, new MarkAskBackReadRequest { LastReadMessageId = m3.Id });
        Assert.Equal(0, all.UnreadCount);

        // The asker's own read state is independent.
        Assert.Equal(1, (await service.GetConversationAsync(scene.Asker.Id, conversationId)).UnreadCount);
    }

    [SkippableFact]
    public async Task MarkRead_WithAMessageOfAnotherConversation_IsRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var (otherScene, otherConversationId) = await StartedAsync(context);
        var service = ServiceFor(context);
        var foreign = await service.SendMessageAsync(otherScene.Asker.Id, otherConversationId, Send("elsewhere"));

        var error = await Assert.ThrowsAsync<AppException>(() => service.MarkReadAsync(
            scene.Asker.Id, conversationId, new MarkAskBackReadRequest { LastReadMessageId = foreign.Id }));

        Assert.Equal(404, error.StatusCode);
    }

    // ---- close, block, mute ----

    [SkippableFact]
    public async Task ClosedConversation_BlocksSending_UntilTheAskerReopens()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var realtime = new RecordingRealtime();
        var service = ServiceFor(context, realtime);
        var kept = Send("sent before closing");
        await service.SendMessageAsync(scene.Asker.Id, conversationId, kept);

        var closed = await service.CloseAsync(scene.Voter.Id, conversationId);

        Assert.Equal("closed", closed.Status);
        Assert.False(closed.CanSend);
        Assert.False(closed.CanReopen);
        Assert.Equal("closed", realtime.Statuses.Single().Status);
        var blocked = await Assert.ThrowsAsync<AppException>(
            () => service.SendMessageAsync(scene.Asker.Id, conversationId, Send("too late")));
        Assert.Equal(409, blocked.StatusCode);

        // A retry of a message that was saved before the close still succeeds.
        Assert.NotNull(await service.SendMessageAsync(scene.Asker.Id, conversationId, kept));
        // History stays readable.
        Assert.Single((await service.ListMessagesAsync(scene.Voter.Id, conversationId, null, null, null)).Items);

        Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(
            () => service.ReopenAsync(scene.Voter.Id, conversationId))).StatusCode);

        var reopened = await service.ReopenAsync(scene.Asker.Id, conversationId);
        Assert.Equal("active", reopened.Status);
        Assert.True(reopened.CanSend);
        await service.SendMessageAsync(scene.Voter.Id, conversationId, Send("back again"));
    }

    [SkippableFact]
    public async Task Block_IsSeparateFromClose_AndOnlyTheBlockerLiftsIt()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);

        var blocked = await service.BlockAsync(
            scene.Voter.Id, conversationId, new BlockAskBackRequest { Reason = "  rude  " });

        Assert.Equal("blocked", blocked.Status);
        Assert.True(blocked.CanUnblock);
        Assert.False((await service.GetConversationAsync(scene.Asker.Id, conversationId)).CanUnblock);
        var report = await context.AskBackReports.SingleAsync(r => r.ConversationId == conversationId);
        Assert.Equal("rude", report.Reason);
        Assert.Equal(scene.Voter.Id, report.ReporterUserId);
        Assert.Equal(scene.Asker.Id, report.ReportedUserId);

        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(
            () => service.SendMessageAsync(scene.Asker.Id, conversationId, Send("hello?")))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(
            () => service.ReopenAsync(scene.Asker.Id, conversationId))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(
            () => service.CloseAsync(scene.Asker.Id, conversationId))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<AppException>(
            () => service.UnblockAsync(scene.Asker.Id, conversationId))).StatusCode);

        var lifted = await service.UnblockAsync(scene.Voter.Id, conversationId);
        Assert.Equal("active", lifted.Status);
        await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("thanks"));
    }

    [SkippableFact]
    public async Task Block_WithoutAReason_RecordsNoReport()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);

        await ServiceFor(context).BlockAsync(scene.Asker.Id, conversationId, new BlockAskBackRequest());

        Assert.Equal(0, await context.AskBackReports.CountAsync(r => r.ConversationId == conversationId));
    }

    [SkippableFact]
    public async Task MutedRecipient_GetsNoPushRow_ButStillGetsTheMessage()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);

        var muted = await service.SetMutedAsync(scene.Voter.Id, conversationId, true);
        await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("quiet"));
        await service.SetMutedAsync(scene.Voter.Id, conversationId, false);
        await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("loud"));

        Assert.True(muted.IsMuted);
        Assert.Equal(1, await context.AskBackOutbox.CountAsync(o => o.ConversationId == conversationId));
        Assert.Equal(2, (await service.ListMessagesAsync(scene.Voter.Id, conversationId, null, null, null)).Items.Count);
        Assert.False((await service.GetConversationAsync(scene.Asker.Id, conversationId)).IsMuted);
    }

    // ---- account lifecycle and reports ----

    [SkippableFact]
    public async Task PermanentlyDeletedAccount_LeavesTheConversationReadOnlyForTheOtherPerson()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);
        await service.SendMessageAsync(scene.Voter.Id, conversationId, Send("my last words"));

        await context.Users.Where(u => u.Id == scene.Voter.Id).ExecuteDeleteAsync();

        var details = await service.GetConversationAsync(scene.Asker.Id, conversationId);
        Assert.Equal("Deleted user", details.Other.AnonymousName);
        Assert.True(details.Other.IsDeleted);
        Assert.False(details.CanSend);
        var history = (await service.ListMessagesAsync(scene.Asker.Id, conversationId, null, null, null)).Items.Single();
        Assert.Equal("my last words", history.Content);
        Assert.Equal("Deleted user", history.SenderName);
        // A fresh context, as every request has: this one still caches the conversation from before the delete.
        await using var fresh = _fixture.CreateContext();
        Assert.Equal(409, (await Assert.ThrowsAsync<AppException>(
            () => ServiceFor(fresh).SendMessageAsync(scene.Asker.Id, conversationId, Send("anyone?")))).StatusCode);
    }

    [SkippableFact]
    public async Task ReportingTheReason_ClosesTheConversation()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);

        await new ReasonRepository(context).ReportPollReasonAsync(scene.Poll.Id, scene.Asker.Id, scene.Vote.Id);

        var details = await ServiceFor(context).GetConversationAsync(scene.Asker.Id, conversationId);
        Assert.Equal("closed", details.Status);
        Assert.Null(details.Context.Reason);
        Assert.True(details.CanReopen);
    }

    // ---- database constraints ----

    [SkippableFact]
    public async Task Database_RefusesASecondConversationForAVote()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, _) = await StartedAsync(context);

        var duplicate = AskBackConversation.Create(
            ShareContentType.Poll, scene.Poll.Id, scene.Vote.Id, scene.Asker.Id, scene.Voter.Id, DateTime.UtcNow);
        await using var other = _fixture.CreateContext();
        other.AskBackConversations.Add(duplicate);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() => other.SaveChangesAsync());
        Assert.Contains("IX_ask_back_conversations_vote_id", error.InnerException!.Message);
    }

    [SkippableFact]
    public async Task Database_RefusesDuplicateParticipants_AndDuplicateClientMessageIds_AndLongContent()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var clientId = Guid.NewGuid();
        context.AskBackMessages.Add(AskBackMessage.Create(conversationId, scene.Asker.Id, clientId, "one", DateTime.UtcNow));
        await context.SaveChangesAsync();

        await using (var dup = _fixture.CreateContext())
        {
            dup.AskBackMessages.Add(AskBackMessage.Create(conversationId, scene.Asker.Id, clientId, "two", DateTime.UtcNow));
            var error = await Assert.ThrowsAsync<DbUpdateException>(() => dup.SaveChangesAsync());
            Assert.Contains("IX_ask_back_messages_idempotency", error.InnerException!.Message);
        }

        // The same client id from the other participant is a different message.
        await using (var other = _fixture.CreateContext())
        {
            other.AskBackMessages.Add(AskBackMessage.Create(conversationId, scene.Voter.Id, clientId, "fine", DateTime.UtcNow));
            await other.SaveChangesAsync();
        }

        await using (var longContent = _fixture.CreateContext())
        {
            longContent.AskBackMessages.Add(AskBackMessage.Create(
                conversationId, scene.Asker.Id, Guid.NewGuid(), new string('z', 2001), DateTime.UtcNow));
            await Assert.ThrowsAnyAsync<DbUpdateException>(() => longContent.SaveChangesAsync());
        }

        var participants = await context.AskBackParticipants
            .AsNoTracking().Where(p => p.ConversationId == conversationId).ToListAsync();
        Assert.Equal(2, participants.Select(p => p.UserId).Distinct().Count());
    }

    // ---- push ----

    [SkippableFact]
    public async Task Push_IsSkippedWhenTheRecipientIsViewing_AndSentWithoutContentOtherwise()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        var service = ServiceFor(context);
        await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("secret words"));
        await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("more secret words"));
        await MakeDueAsync(context, conversationId);

        var push = new RecordingPush();

        // Viewing: nothing is sent for this conversation and its rows are closed out as skipped.
        await DrainAsync(context, conversationId, push, new FixedPresence(conversationId, scene.Voter.Id));

        Assert.Empty(push.For(conversationId));
        Assert.All(
            await context.AskBackOutbox.AsNoTracking().Where(o => o.ConversationId == conversationId).ToListAsync(),
            o => Assert.Equal(AskBackOutboxStatus.Skipped, o.Status));

        // Not viewing: a generic push with the conversation id and no message text.
        await service.SendMessageAsync(scene.Asker.Id, conversationId, Send("third secret"));
        await MakeDueAsync(context, conversationId);
        await DrainAsync(context, conversationId, push, new NullAskBackPresence());

        var (userId, payload) = Assert.Single(push.For(conversationId));
        Assert.Equal(scene.Voter.Id, userId);
        Assert.Equal("AskBackMessage", payload.Data!["eventType"]);
        var serialized = JsonSerializer.Serialize(payload);
        Assert.DoesNotContain("secret", serialized);
        Assert.DoesNotContain(scene.Asker.Profile.AnonymousName, serialized);
        Assert.DoesNotContain(scene.Asker.PhoneNumber, serialized);
    }

    [SkippableFact]
    public async Task Push_ClaimedByOneWorker_IsNotSentTwice()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var (scene, conversationId) = await StartedAsync(context);
        await ServiceFor(context).SendMessageAsync(scene.Asker.Id, conversationId, Send("once"));
        await MakeDueAsync(context, conversationId);
        var push = new RecordingPush();

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
            {
                await using var run = _fixture.CreateContext();
                await new AskBackPushDispatcher(
                    run, push, new NullAskBackPresence(), TimeProvider.System, NullLogger<AskBackPushDispatcher>.Instance)
                    .ProcessAsync(default);
            }));
            if (!await context.AskBackOutbox.AnyAsync(o => o.ConversationId == conversationId && o.Status != AskBackOutboxStatus.Sent))
            {
                break;
            }
        }

        Assert.Single(push.For(conversationId));
        Assert.Equal(
            AskBackOutboxStatus.Sent,
            (await context.AskBackOutbox.AsNoTracking().SingleAsync(o => o.ConversationId == conversationId)).Status);
    }

    private static Task<int> MakeDueAsync(YakkuDbContext context, Guid conversationId)
    {
        return context.AskBackOutbox
            .Where(o => o.ConversationId == conversationId && o.Status == AskBackOutboxStatus.Pending)
            .ExecuteUpdateAsync(set => set.SetProperty(o => o.NextAttemptAt, DateTime.UtcNow.AddMinutes(-5)));
    }

    /// <summary>The database is shared with other tests, so run the dispatcher until this conversation's rows are done.</summary>
    private async Task DrainAsync(
        YakkuDbContext context,
        Guid conversationId,
        IPushNotificationService push,
        IAskBackPresence presence)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            await using var run = _fixture.CreateContext();
            await new AskBackPushDispatcher(
                run, push, presence, TimeProvider.System, NullLogger<AskBackPushDispatcher>.Instance)
                .ProcessAsync(default);
            var open = await context.AskBackOutbox.AsNoTracking().AnyAsync(o =>
                o.ConversationId == conversationId
                && (o.Status == AskBackOutboxStatus.Pending || o.Status == AskBackOutboxStatus.Processing));
            if (!open)
            {
                return;
            }
        }
    }


    // ---- helpers ----

    private sealed record Scene(User Asker, User Voter, Poll Poll, PollVote Vote);

    private static SendAskBackMessageRequest Send(string content)
    {
        return new SendAskBackMessageRequest { ClientMessageId = Guid.NewGuid(), Content = content };
    }

    private static AskBackService ServiceFor(YakkuDbContext context, IAskBackRealtime? realtime = null)
    {
        return new AskBackService(
            new AskBackRepository(context),
            realtime ?? new NullAskBackRealtime(),
            new CreateAskBackConversationRequestValidator(),
            new SendAskBackMessageRequestValidator(),
            new MarkAskBackReadRequestValidator(),
            new BlockAskBackRequestValidator(),
            TimeProvider.System,
            NullLogger<AskBackService>.Instance);
    }

    /// <summary>An asker, a poll with two options, and one other user's vote with a reason.</summary>
    private static async Task<Scene> SceneAsync(YakkuDbContext context)
    {
        var asker = await AddUserAsync(context);
        var voter = await AddUserAsync(context);
        var poll = await AddPollAsync(context, asker.Id);
        var vote = PollVote.ForOption(voter.Id, poll.Id, poll.Options.First().Id, "Because it is clearer");
        context.PollVotes.Add(vote);
        await context.SaveChangesAsync();
        return new Scene(asker, voter, poll, vote);
    }

    private static async Task<(Scene Scene, Guid ConversationId)> StartedAsync(YakkuDbContext context)
    {
        var scene = await SceneAsync(context);
        var (conversation, _) = await ServiceFor(context).CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id });
        return (scene, conversation.Id);
    }

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"571{sequence:D10}", $"Real Name {sequence}", $"askback-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<Poll> AddPollAsync(YakkuDbContext context, Guid creatorId)
    {
        var poll = new Poll(creatorId, "Which presentation is better?", PollContentType.Question);
        poll.AddTextOption("Option A", 1);
        poll.AddTextOption("Option B", 2);
        poll.MarkPublished();
        await new PollRepository(context).AddAsync(poll);
        await context.SaveChangesAsync();
        return poll;
    }

    private sealed class RecordingRealtime : IAskBackRealtime
    {
        public bool Throw { get; init; }
        public List<(Guid Recipient, AskBackMessageResponse ForSender, AskBackMessageResponse ForRecipient)> Messages { get; } = new();
        public List<(Guid? Other, Guid MessageId)> Reads { get; } = new();
        public List<(Guid ConversationId, string Status)> Statuses { get; } = new();

        public Task MessageCreatedAsync(Guid conversationId, Guid senderUserId, Guid? recipientUserId, AskBackMessageResponse forSender, AskBackMessageResponse forRecipient, CancellationToken cancellationToken = default)
        {
            if (Throw)
            {
                throw new InvalidOperationException("hub down");
            }

            Messages.Add((recipientUserId!.Value, forSender, forRecipient));
            return Task.CompletedTask;
        }

        public Task MessageReadAsync(Guid conversationId, Guid? otherUserId, Guid lastReadMessageId, DateTime readAt, CancellationToken cancellationToken = default)
        {
            Reads.Add((otherUserId, lastReadMessageId));
            return Task.CompletedTask;
        }

        public Task StatusChangedAsync(Guid conversationId, IReadOnlyCollection<Guid> userIds, string status, CancellationToken cancellationToken = default)
        {
            Statuses.Add((conversationId, status));
            return Task.CompletedTask;
        }
    }

    private sealed class FixedPresence(Guid conversationId, Guid userId) : IAskBackPresence
    {
        public bool IsViewing(Guid conversation, Guid user) => conversation == conversationId && user == userId;
    }

    private sealed class RecordingPush : IPushNotificationService
    {
        public List<(Guid UserId, NotificationPayload Payload)> Sent { get; } = new();

        /// <summary>The pushes about one conversation (other tests' rows share the database).</summary>
        public List<(Guid UserId, NotificationPayload Payload)> For(Guid conversationId)
        {
            lock (Sent)
            {
                return Sent
                    .Where(item => item.Payload.Data?.GetValueOrDefault("conversationId") == conversationId.ToString())
                    .ToList();
            }
        }

        public Task<PushUserNotificationResult> SendToUserAsync(Guid userId, NotificationPayload payload, CancellationToken cancellationToken = default)
        {
            lock (Sent)
            {
                Sent.Add((userId, payload));
            }

            return Task.FromResult(new PushUserNotificationResult { UserId = userId, TotalDevices = 1, EligibleDevices = 1, Successful = 1 });
        }

        public Task<PushDeviceNotificationResult> SendToDeviceAsync(Device device, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PushUserNotificationResult> SendToDevicesAsync(IReadOnlyList<Device> devices, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PushDeviceNotificationResult> SendToDeviceAsync(string installationId, string platform, NotificationPayload payload, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PushDeviceNotificationResult> SendTestToDeviceAsync(TestDeviceNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<PushUserNotificationResult> SendTestToUserAsync(TestUserNotificationRequest request, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
