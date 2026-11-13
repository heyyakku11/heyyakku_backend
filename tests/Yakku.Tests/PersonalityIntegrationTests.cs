using static Yakku.Tests.PersonalityTestIds;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Personality.DTOs;
using Yakku.Application.Personality.Services;
using Yakku.Application.Personality.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Persistence;
using Yakku.Infrastructure.Persistence.Repositories;

namespace Yakku.Tests;

/// <summary>Personality cards against real PostgreSQL: constraints, locking and the full lifecycle. Needs Docker.</summary>
[Collection(PostgresCollection.Name)]
public sealed class PersonalityIntegrationTests
{
    private static int _sequence;
    private readonly PostgresFixture _fixture;

    public PersonalityIntegrationTests(PostgresFixture fixture)
    {
        _fixture = fixture;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, string> ShareCodes = new();

    /// <summary>The share code of a card the test created: the endpoints open cards by code, not by id.</summary>
    private static string Code(Guid cardId) => ShareCodes[cardId];

    private static async Task<Guid> CardIdOfAsync(YakkuDbContext context, Guid userId)
    {
        var card = await context.PersonalityCards.AsNoTracking().SingleAsync(c => c.UserId == userId);
        ShareCodes[card.Id] = card.ShareCode;
        return card.Id;
    }

    private static SubmitTraitsRequest Traits(params int[] ids) => new() { TraitIds = ids.Select(T).ToList() };

    private static PersonalityService ServiceFor(YakkuDbContext context) =>
        new(new PersonalityRepository(context), new SubmitTraitsValidator());

    private static async Task<User> AddUserAsync(YakkuDbContext context)
    {
        var sequence = Interlocked.Increment(ref _sequence);
        var user = new User($"559{sequence:D10}", $"personality-anon-{sequence}");
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    /// <summary>A card in Collecting with the given number of votes already cast.</summary>
    private async Task<(User Owner, Guid CardId)> CollectingCardAsync(int existingVotes)
    {
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        await ServiceFor(context).SubmitSelfAssessmentAsync(owner.Id, Traits(1, 2, 3));
        var cardId = await CardIdOfAsync(context, owner.Id);

        for (var i = 0; i < existingVotes; i++)
        {
            await using var voterContext = _fixture.CreateContext();
            var voter = await AddUserAsync(voterContext);
            await ServiceFor(voterContext).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4, 8));
        }

        return (owner, cardId);
    }

    private async Task<AppException> ExpectErrorAsync(Func<Task> action)
    {
        return await Assert.ThrowsAsync<AppException>(action);
    }

    // ---- registration / seed / backfill -----------------------------------------------------------------------

    [SkippableFact]
    public async Task SavingANewUser_CreatesExactlyOneDraftCard_InTheSameSave()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var user = await AddUserAsync(context);

        var cards = await context.PersonalityCards.AsNoTracking().Where(c => c.UserId == user.Id).ToListAsync();
        var card = Assert.Single(cards);
        Assert.Equal(PersonalityCardStatus.Draft, card.Status);
        Assert.Equal(5, card.RequiredVotes);
        Assert.Equal(0, card.TotalVotes);
        Assert.Null(card.CompletedAt);
    }

    [SkippableFact]
    public async Task AFailedRegistration_LeavesNoOrphanCard()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var setup = _fixture.CreateContext();
        var existing = await AddUserAsync(setup);

        await using var context = _fixture.CreateContext();
        // Same phone number: the user insert fails, so the card in the same save must not survive.
        var duplicate = new User(existing.PhoneNumber, "personality-dup-name");
        context.Users.Add(duplicate);
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        await using var check = _fixture.CreateContext();
        Assert.False(await check.PersonalityCards.AnyAsync(c => c.UserId == duplicate.Id));
    }

    [SkippableFact]
    public async Task ASecondCardForTheSameUser_IsRejectedByTheDatabase()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);

        context.PersonalityCards.Add(new PersonalityCard(user.Id));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());

        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
    }

    [SkippableFact]
    public async Task EnsureCard_IsIdempotent()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var repository = new PersonalityRepository(context);

        await repository.EnsureCardAsync(user.Id);
        await repository.EnsureCardAsync(user.Id);

        Assert.Equal(1, await context.PersonalityCards.CountAsync(c => c.UserId == user.Id));
    }

    [SkippableFact]
    public async Task TheTenTraitsAreSeeded_WithStableIds_AndTheSeedCanRunAgain()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();

        var traits = await context.PersonalityTraits.AsNoTracking().OrderBy(t => t.Id).ToListAsync();

        Assert.Equal(Enumerable.Range(1, 10), traits.Select(t => N(t.Id)));
        Assert.Equal("Confident", traits[0].Name);
        Assert.Equal("Reliable", traits[9].Name);
        Assert.All(traits, t => Assert.True(t.IsActive));

        // The migration's seed statement uses ON CONFLICT DO NOTHING: running it again adds nothing.
        var inserted = await context.Database.ExecuteSqlRawAsync(
            "INSERT INTO personality_traits (id, name, description, is_active, display_order) VALUES (1, 'Confident', 'x', true, 1) ON CONFLICT (id) DO NOTHING");
        Assert.Equal(0, inserted);
        Assert.Equal(10, await context.PersonalityTraits.CountAsync());
    }

    [SkippableFact]
    public async Task TheBackfillStatement_AddsCardsOnlyForUsersWithoutOne()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM personality_cards WHERE user_id = {user.Id}");

        const string backfill = """
            INSERT INTO personality_cards (id, user_id, status, required_votes, total_votes, created_at, updated_at)
            SELECT gen_random_uuid(), u."Id", 'Draft', 5, 0, now(), now()
            FROM users u
            ON CONFLICT (user_id) DO NOTHING
            """;
        await context.Database.ExecuteSqlRawAsync(backfill);
        var second = await context.Database.ExecuteSqlRawAsync(backfill);

        Assert.Equal(0, second);
        Assert.Equal(1, await context.PersonalityCards.CountAsync(c => c.UserId == user.Id));
    }

    // ---- self-assessment --------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task SelfAssessment_SavesThreeTraits_AndMovesTheCardToCollecting()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);

        var result = await ServiceFor(context).SubmitSelfAssessmentAsync(user.Id, Traits(3, 1, 2));

        Assert.Equal("Collecting", result.Status);
        Assert.Matches("^[0-9a-f]{6}$", result.ShareCode);
        var mine = await ServiceFor(context).GetMyCardAsync(user.Id);
        Assert.Equal(new[] { 1, 2, 3 }, mine.SelfTraits.Select(t => N(t.Id)));
    }

    [SkippableFact]
    public async Task SelfAssessment_CannotBeSubmittedTwice_AndTheFirstOneIsKept()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        await ServiceFor(context).SubmitSelfAssessmentAsync(user.Id, Traits(1, 2, 3));

        await using var second = _fixture.CreateContext();
        var error = await ExpectErrorAsync(() => ServiceFor(second).SubmitSelfAssessmentAsync(user.Id, Traits(4, 5, 6)));

        Assert.Equal(409, error.StatusCode);
        var mine = await ServiceFor(second).GetMyCardAsync(user.Id);
        Assert.Equal(new[] { 1, 2, 3 }, mine.SelfTraits.Select(t => N(t.Id)));
    }

    [SkippableFact]
    public async Task SelfAssessment_RejectsWrongCountDuplicatesAndUnknownTraits()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        var service = ServiceFor(context);

        await Assert.ThrowsAnyAsync<Exception>(() => service.SubmitSelfAssessmentAsync(user.Id, Traits(1, 2)));
        await Assert.ThrowsAnyAsync<Exception>(() => service.SubmitSelfAssessmentAsync(user.Id, Traits(1, 2, 3, 4)));
        await Assert.ThrowsAnyAsync<Exception>(() => service.SubmitSelfAssessmentAsync(user.Id, Traits(1, 1, 2)));
        var unknown = await ExpectErrorAsync(() => service.SubmitSelfAssessmentAsync(user.Id, Traits(1, 2, 999)));
        Assert.Equal(400, unknown.StatusCode);

        Assert.Equal("Draft", (await service.GetMyCardAsync(user.Id)).Status);
    }

    [SkippableFact]
    public async Task InactiveTraits_AreNotListed_AndAreRejectedInNewSubmissions()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        try
        {
            await context.Database.ExecuteSqlRawAsync($"UPDATE personality_traits SET is_active = false WHERE id = '{T(10)}'");

            var listed = await ServiceFor(context).GetTraitsAsync();
            Assert.DoesNotContain(listed, t => N(t.Id) == 10);
            Assert.Equal(9, listed.Count);

            var error = await ExpectErrorAsync(() => ServiceFor(context).SubmitSelfAssessmentAsync(user.Id, Traits(1, 2, 10)));
            Assert.Equal(400, error.StatusCode);
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync($"UPDATE personality_traits SET is_active = true WHERE id = '{T(10)}'");
        }
    }

    // ---- voting -----------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task AValidVote_IsAccepted_AndCountsOnTheCard()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(0);
        await using var context = _fixture.CreateContext();
        var voter = await AddUserAsync(context);

        var result = await ServiceFor(context).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4, 8));

        Assert.Equal(1, result.TotalVotes);
        Assert.Equal("Collecting", result.CardStatus);
        var vote = await context.PersonalityVotes.Include(v => v.Traits).AsNoTracking().SingleAsync(v => v.VoterUserId == voter.Id);
        Assert.Equal(3, vote.Traits.Count);
    }

    [SkippableFact]
    public async Task ADuplicateVote_IsRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(0);
        await using var context = _fixture.CreateContext();
        var voter = await AddUserAsync(context);
        await ServiceFor(context).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4, 8));

        await using var again = _fixture.CreateContext();
        var error = await ExpectErrorAsync(() => ServiceFor(again).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(2, 5, 9)));

        Assert.Equal(409, error.StatusCode);
        Assert.Equal(1, await again.PersonalityCards.Where(c => c.Id == cardId).Select(c => c.TotalVotes).SingleAsync());
    }

    [SkippableFact]
    public async Task VotingOnYourOwnCard_IsRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (owner, cardId) = await CollectingCardAsync(0);
        await using var context = _fixture.CreateContext();

        var error = await ExpectErrorAsync(() => ServiceFor(context).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(owner.Id), Traits(1, 4, 8)));

        Assert.Equal(403, error.StatusCode);
    }

    [SkippableFact]
    public async Task VotingOnADraftCard_IsRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        var voter = await AddUserAsync(context);
        var cardId = await CardIdOfAsync(context, owner.Id);

        var error = await ExpectErrorAsync(() => ServiceFor(context).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4, 8)));

        Assert.Equal(409, error.StatusCode);
    }

    [SkippableFact]
    public async Task AVoteWithABadTraitSelection_IsRejected_AndNothingIsSaved()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(0);
        await using var context = _fixture.CreateContext();
        var voter = await AddUserAsync(context);
        var service = ServiceFor(context);

        await Assert.ThrowsAnyAsync<Exception>(() => service.VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4)));
        await Assert.ThrowsAnyAsync<Exception>(() => service.VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4, 4)));
        await Assert.ThrowsAnyAsync<Exception>(() => service.VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4, 999)));

        Assert.False(await context.PersonalityVotes.AnyAsync(v => v.PersonalityCardId == cardId));
        Assert.Equal(0, await context.PersonalityCards.Where(c => c.Id == cardId).Select(c => c.TotalVotes).SingleAsync());
    }

    [SkippableFact]
    public async Task TheDatabaseItself_RejectsTwoVotesFromOneUser_AndRepeatedVoteTraits()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(0);
        await using var context = _fixture.CreateContext();
        var voter = await AddUserAsync(context);
        context.PersonalityVotes.Add(new PersonalityVote(cardId, voter.Id, false, new[] { 1, 2, 3 }.Select(T)));
        await context.SaveChangesAsync();

        // Bypasses the service entirely: only the unique index stands in the way.
        await using var second = _fixture.CreateContext();
        second.PersonalityVotes.Add(new PersonalityVote(cardId, voter.Id, false, new[] { 4, 5, 6 }.Select(T)));
        var duplicateVote = await Assert.ThrowsAsync<DbUpdateException>(() => second.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicateVote.InnerException!).SqlState);

        await using var third = _fixture.CreateContext();
        var otherVoter = await AddUserAsync(third);
        third.PersonalityVotes.Add(new PersonalityVote(cardId, otherVoter.Id, false, new[] { 7, 7, 8 }.Select(T)));
        var duplicateTrait = await Assert.ThrowsAsync<DbUpdateException>(() => third.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)duplicateTrait.InnerException!).SqlState);
        Assert.False(await third.PersonalityVotes.AnyAsync(v => v.VoterUserId == otherVoter.Id));
    }

    // ---- completion -------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task FourVotesKeepCollecting_TheFifthCompletes_TheSixthIsRejected()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(4);

        await using (var check = _fixture.CreateContext())
        {
            var card = await check.PersonalityCards.AsNoTracking().SingleAsync(c => c.Id == cardId);
            Assert.Equal(PersonalityCardStatus.Collecting, card.Status);
            Assert.Equal(4, card.TotalVotes);
        }

        await using var context = _fixture.CreateContext();
        var fifth = await AddUserAsync(context);
        var result = await ServiceFor(context).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(fifth.Id), Traits(1, 4, 8));
        Assert.Equal("Completed", result.CardStatus);
        Assert.NotNull(result.CompletedAt);

        await using var sixthContext = _fixture.CreateContext();
        var sixth = await AddUserAsync(sixthContext);
        var error = await ExpectErrorAsync(() => ServiceFor(sixthContext).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(sixth.Id), Traits(1, 4, 8)));
        Assert.Equal(409, error.StatusCode);

        Assert.Equal(5, await sixthContext.PersonalityVotes.CountAsync(v => v.PersonalityCardId == cardId));
    }

    [SkippableFact]
    public async Task TwoSimultaneousFifthVotes_OnlyOneIsAccepted()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(4);

        await using var setup = _fixture.CreateContext();
        var voters = new List<User>();
        for (var i = 0; i < 6; i++)
        {
            voters.Add(await AddUserAsync(setup));
        }

        var attempts = voters.Select(async voter =>
        {
            await using var context = _fixture.CreateContext();
            try
            {
                await ServiceFor(context).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4, 8));
                return true;
            }
            catch (AppException)
            {
                return false;
            }
        }).ToList();
        var outcomes = await Task.WhenAll(attempts);

        Assert.Equal(1, outcomes.Count(accepted => accepted));
        await using var check = _fixture.CreateContext();
        var card = await check.PersonalityCards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        Assert.Equal(PersonalityCardStatus.Completed, card.Status);
        Assert.Equal(5, card.TotalVotes);
        Assert.Equal(5, await check.PersonalityVotes.CountAsync(v => v.PersonalityCardId == cardId));
    }

    [SkippableFact]
    public async Task TheSameUserVotingTwiceAtOnce_IsCountedOnce()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(0);
        await using var setup = _fixture.CreateContext();
        var voter = await AddUserAsync(setup);

        var outcomes = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var context = _fixture.CreateContext();
            try
            {
                await ServiceFor(context).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4, 8));
                return true;
            }
            catch (AppException)
            {
                return false;
            }
        }));

        Assert.Equal(1, outcomes.Count(accepted => accepted));
        Assert.Equal(1, await setup.PersonalityCards.AsNoTracking().Where(c => c.Id == cardId).Select(c => c.TotalVotes).SingleAsync());
    }

    // ---- results ----------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task Results_AreUnavailableBeforeCompletion_AndOwnerOnly()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (owner, cardId) = await CollectingCardAsync(2);
        await using var context = _fixture.CreateContext();
        var stranger = await AddUserAsync(context);

        var early = await ExpectErrorAsync(() => ServiceFor(context).GetResultsAsync(Code(cardId), owner.Id));
        var notOwner = await ExpectErrorAsync(() => ServiceFor(context).GetResultsAsync(Code(cardId), stranger.Id));

        Assert.Equal(409, early.StatusCode);
        Assert.Equal(403, notOwner.StatusCode);
    }

    [SkippableFact]
    public async Task Results_CompareSelfWithTheCrowd_WithoutExposingVoters()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var owner = await AddUserAsync(context);
        await ServiceFor(context).SubmitSelfAssessmentAsync(owner.Id, Traits(1, 3, 5));
        var cardId = await CardIdOfAsync(context, owner.Id);

        // 1 and 10 get 4 votes each, 3 gets 3, 6 gets 2, 8 and 5 get 1 (the spec's example).
        var picks = new[]
        {
            new[] { 1, 10, 3 }, new[] { 1, 10, 3 }, new[] { 1, 10, 3 }, new[] { 1, 10, 6 }, new[] { 6, 8, 5 }
        };
        Assert.Equal(4, picks.Count(p => p.Contains(1)));
        foreach (var pick in picks)
        {
            await using var voterContext = _fixture.CreateContext();
            var voter = await AddUserAsync(voterContext);
            await ServiceFor(voterContext).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(pick));
        }

        var results = await ServiceFor(context).GetResultsAsync(Code(cardId), owner.Id);

        Assert.Equal(new[] { 1, 10, 3 }, results.CrowdTopTraits.Select(t => N(t.Id)));
        Assert.Equal(new[] { 4, 4, 3 }, results.CrowdTopTraits.Select(t => t.Votes));
        Assert.Equal(new[] { 1, 3 }, results.CommonTraits.Select(t => N(t.Id)));
        Assert.Equal(new[] { 5 }, results.SelfOnlyTraits.Select(t => N(t.Id)));
        Assert.Equal(new[] { 10 }, results.CrowdOnlyTraits.Select(t => N(t.Id)));
        Assert.Equal(new[] { 1, 3, 5 }, results.SelfSelectedTraits.Select(t => N(t.Id)));

        // No property of the payload can carry a voter: only trait ids, names, counts and the card's own numbers.
        var properties = typeof(PersonalityResultsResponse).GetProperties().Select(p => p.Name);
        Assert.DoesNotContain(properties, name => name.Contains("Voter", StringComparison.OrdinalIgnoreCase));
    }

    [SkippableFact]
    public async Task TheSharedCard_HidesTheOwnersPicks_AndDraftsAreNotFound()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var draftOwner = await AddUserAsync(context);
        var viewer = await AddUserAsync(context);
        var draftId = await CardIdOfAsync(context, draftOwner.Id);

        var hidden = await ExpectErrorAsync(() => ServiceFor(context).GetSharedCardAsync(Code(draftId), Yakku.Application.Sharing.VoterIdentity.ForUser(viewer.Id)));
        Assert.Equal(404, hidden.StatusCode);

        var (_, cardId) = await CollectingCardAsync(0);
        var shared = await ServiceFor(context).GetSharedCardAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(viewer.Id));
        Assert.True(shared.CanVote);
        Assert.False(shared.IsOwner);
        Assert.DoesNotContain(typeof(SharedPersonalityCardResponse).GetProperties(), p => p.Name.Contains("SelfTraits"));
    }

    // ---- share codes ------------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task EnsureCard_RegistersTheCardsShareCodeInTheSharedRegistry()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        await using var context = _fixture.CreateContext();
        var user = await AddUserAsync(context);
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM personality_cards WHERE user_id = {user.Id}");

        await new PersonalityRepository(context).EnsureCardAsync(user.Id);

        var card = await context.PersonalityCards.AsNoTracking().SingleAsync(c => c.UserId == user.Id);
        Assert.Matches("^[0-9a-f]{6}$", card.ShareCode);
        var entry = await context.ShareCodes.AsNoTracking().SingleAsync(s => s.Code == card.ShareCode);
        Assert.Equal(ShareContentType.Personality, entry.ContentType);
        Assert.Equal(card.Id, entry.PersonalityCardId);
    }

    [SkippableFact]
    public async Task ACardIsOpenedByItsShareCode_AndAnUnknownCodeIsNotFound()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(0);
        await using var context = _fixture.CreateContext();
        var viewer = await AddUserAsync(context);

        var shared = await ServiceFor(context).GetSharedCardAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(viewer.Id));
        Assert.Equal(cardId, shared.CardId);
        Assert.Equal(Code(cardId), shared.ShareCode);

        foreach (var bad in new[] { "ffffff", "", "   ", new string('a', 65) })
        {
            var error = await ExpectErrorAsync(() => ServiceFor(context).GetSharedCardAsync(bad, Yakku.Application.Sharing.VoterIdentity.ForUser(viewer.Id)));
            Assert.Equal(404, error.StatusCode);
        }
    }

    [SkippableFact]
    public async Task TheDatabaseRejectsADuplicateCardShareCode_AndCodesAreUniqueAcrossContentTypes()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(0);
        await using var context = _fixture.CreateContext();
        var other = await AddUserAsync(context);

        // Another card with the same code: the unique index on the card stops it.
        var duplicate = new PersonalityCard(Guid.NewGuid(), Code(cardId));
        context.PersonalityCards.Add(duplicate);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)exception.InnerException!).SqlState);
        context.ChangeTracker.Clear();

        // The registry key is shared with polls and checks, so a registered code cannot be reused by anything else.
        var raw = await Assert.ThrowsAsync<PostgresException>(() => context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO share_codes (code, content_type, personality_card_id) SELECT {Code(cardId)}, 'PERSONALITY', id FROM personality_cards WHERE user_id = {other.Id}"));
        Assert.Equal(PostgresErrorCodes.UniqueViolation, raw.SqlState);
    }

    // ---- account deletion -------------------------------------------------------------------------------------

    [SkippableFact]
    public async Task PurgingAVoter_RemovesTheirVotes_AndKeepsCardCountersConsistent()
    {
        Skip.IfNot(_fixture.IsAvailable, _fixture.SkipReason);
        var (_, cardId) = await CollectingCardAsync(0);
        await using var context = _fixture.CreateContext();
        var voter = await AddUserAsync(context);
        await ServiceFor(context).VoteAsync(Code(cardId), Yakku.Application.Sharing.VoterIdentity.ForUser(voter.Id), Traits(1, 4, 8));

        voter.RequestDeletion(DateTime.UtcNow.AddDays(-31), TimeSpan.FromDays(30));
        await context.SaveChangesAsync();
        var result = await new AccountDeletionRepository(context).PurgeAsync(voter.Id, DateTime.UtcNow);

        Assert.True(result.Deleted);
        await using var check = _fixture.CreateContext();
        var card = await check.PersonalityCards.AsNoTracking().SingleAsync(c => c.Id == cardId);
        Assert.Equal(0, card.TotalVotes);
        Assert.Equal(card.TotalVotes, await check.PersonalityVotes.CountAsync(v => v.PersonalityCardId == cardId));
        Assert.False(await check.PersonalityCards.AnyAsync(c => c.UserId == voter.Id));
    }
}
