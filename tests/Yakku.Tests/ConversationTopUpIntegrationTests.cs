using Microsoft.EntityFrameworkCore;
using Yakku.Application.AskBack.DTOs;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Payments;
using Yakku.Application.Payments.Interfaces;
using Yakku.Application.Payments.Services;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>
/// Conversation top-ups: bought by the asker for one chat whose questions ran out, granted by a captured payment,
/// and never a change to the user's plan.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ConversationTopUpIntegrationTests
{
    private readonly PostgresFixture _fixture;

    public ConversationTopUpIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    [SkippableFact]
    public async Task TopUp_AddsQuestionsToTheExhaustedChat_AndNeverChangesThePlan()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await AskBackIntegrationTests.SceneAsync(context);
        // A card bought on its own gives one question per reason.
        await AskBackIntegrationTests.EntitlementsFor(context).GrantCardUnlockAsync(scene.Asker.Id, "poll", scene.Poll.Id);
        var service = AskBackIntegrationTests.ServiceFor(context);
        var (conversation, _) = await service.CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id });
        await service.SendMessageAsync(scene.Asker.Id, conversation.Id, AskBackIntegrationTests.Send("Why?"));
        await service.SendMessageAsync(scene.Voter.Id, conversation.Id, AskBackIntegrationTests.Send("Because."));
        Assert.Equal("done", (await service.GetConversationAsync(scene.Asker.Id, conversation.Id)).Turn);

        // The questions are used up, so the asker may top the chat up.
        await service.EnsureCanTopUpAsync(scene.Asker.Id, conversation.Id);
        var order = await SeedTopUpOrderAsync(context, scene.Asker.Id, conversation.Id, Plan.TopUp19Id);
        var paymentId = $"pay_topup_{Guid.NewGuid():N}";
        await FulfillAsync(context, order, paymentId);

        // Three questions were added to this chat only.
        var after = await service.GetConversationAsync(scene.Asker.Id, conversation.Id);
        Assert.Equal("me", after.Turn);
        Assert.Equal(3, after.MessagesLeft);
        Assert.True(after.CanSend);
        Assert.Null((await service.GetConversationAsync(scene.Voter.Id, conversation.Id)).MessagesLeft);

        // A top-up never changes the user's plan.
        Assert.Equal(
            Plan.FreeCode,
            await context.UserPlans.AsNoTracking().Where(p => p.UserId == scene.Asker.Id).Select(p => p.PlanCode).SingleAsync());

        // Each question spends one top-up question.
        await service.SendMessageAsync(scene.Asker.Id, conversation.Id, AskBackIntegrationTests.Send("More?"));
        Assert.Equal(2, (await service.GetConversationAsync(scene.Asker.Id, conversation.Id)).MessagesLeft);

        // A retried fulfillment of the same payment grants nothing new.
        await FulfillAsync(context, order, paymentId);
        Assert.Equal(1, await context.AskBackConversationTopUps.CountAsync(t => t.PaymentOrderId == order.Id));
        Assert.Equal(2, (await service.GetConversationAsync(scene.Asker.Id, conversation.Id)).MessagesLeft);
    }

    [SkippableFact]
    public async Task TopUp_IsRefused_WhileTheChatStillHasQuestions_ForTheVoter_AndForAStranger()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var scene = await AskBackIntegrationTests.SceneAsync(context);
        await AskBackIntegrationTests.EntitlementsFor(context).GrantCardUnlockAsync(scene.Asker.Id, "poll", scene.Poll.Id);
        var service = AskBackIntegrationTests.ServiceFor(context);
        var (conversation, _) = await service.CreateConversationAsync(
            scene.Asker.Id, new CreateAskBackConversationRequest { VoteId = scene.Vote.Id });

        // One question is still left, so there is nothing to top up yet.
        var stillHas = await Assert.ThrowsAsync<AppException>(
            () => service.EnsureCanTopUpAsync(scene.Asker.Id, conversation.Id));
        Assert.Equal(409, stillHas.StatusCode);
        Assert.Equal(ApiErrorCodes.AskBackTopUpNotAvailable, stillHas.ErrorCode);

        // Only the asker pays for a top-up.
        var voter = await Assert.ThrowsAsync<AppException>(
            () => service.EnsureCanTopUpAsync(scene.Voter.Id, conversation.Id));
        Assert.Equal(ApiErrorCodes.AskBackTopUpNotAvailable, voter.ErrorCode);

        // Someone who is not in the chat cannot see it at all.
        var stranger = await AskBackIntegrationTests.AddUserAsync(context);
        var notFound = await Assert.ThrowsAsync<AppException>(
            () => service.EnsureCanTopUpAsync(stranger.Id, conversation.Id));
        Assert.Equal(404, notFound.StatusCode);
    }

    private static async Task<PaymentOrder> SeedTopUpOrderAsync(
        YakkuDbContext context,
        Guid askerId,
        Guid conversationId,
        Guid planId)
    {
        var plan = await context.Plans.AsNoTracking().SingleAsync(p => p.Id == planId);
        var order = PaymentOrder.CreateConversationTopUp(
            askerId,
            plan.Id,
            conversationId,
            plan.PriceMinor,
            plan.Currency,
            PlanPurchaseSnapshot.FromPlan(plan).ToJson(),
            $"idem-topup-{Guid.NewGuid():N}",
            DateTime.UtcNow,
            TimeSpan.FromMinutes(15),
            "topup-test");
        order.AttachRazorpayOrder($"order_{Guid.NewGuid():N}", DateTime.UtcNow, TimeSpan.FromMinutes(15));
        context.PaymentOrders.Add(order);
        await context.SaveChangesAsync();
        return order;
    }

    private static async Task FulfillAsync(YakkuDbContext context, PaymentOrder order, string paymentId)
    {
        var service = new PaymentFulfillmentService(new PaymentFulfillmentRepository(context), new NullPaymentLogWriter());
        await service.FulfillCapturedPaymentAsync(
            new CapturedPaymentFulfillmentCommand(
                order.Id,
                order.RazorpayOrderId!,
                paymentId,
                order.AmountInPaise,
                order.Currency,
                "upi",
                "captured",
                DateTime.UtcNow,
                "topup-test"));

        // The grant updates the chat with a bulk statement; drop tracked copies so reads see it.
        context.ChangeTracker.Clear();
    }
}
