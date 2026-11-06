using Microsoft.EntityFrameworkCore;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence;

namespace Yakku.Tests;

/// <summary>
/// EF must treat the rows a card creates for itself as new (INSERT), not as existing (UPDATE, which affects 0 rows
/// and throws DbUpdateConcurrencyException). No database is needed: only the tracked state is inspected.
/// </summary>
public sealed class PersonalityTrackingTests
{
    private static YakkuDbContext NewContext() =>
        new(new DbContextOptionsBuilder<YakkuDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);

    [Fact]
    public void SelfTraits_AddedToAnAlreadyTrackedCard_AreTrackedAsNew()
    {
        using var context = NewContext();
        var card = new PersonalityCard(Guid.NewGuid());
        context.Attach(card); // as when the card was loaded from the database

        card.SubmitSelfAssessment(new[] { PersonalityTraitIds.Creative, PersonalityTraitIds.Intelligent, PersonalityTraitIds.Calm });
        context.ChangeTracker.DetectChanges();

        Assert.Equal(3, context.ChangeTracker.Entries<PersonalityCardSelfTrait>().Count());
        Assert.All(context.ChangeTracker.Entries<PersonalityCardSelfTrait>(), entry => Assert.Equal(EntityState.Added, entry.State));
    }

    [Fact]
    public void AVoteAndItsTraits_AreTrackedAsNew()
    {
        using var context = NewContext();

        var vote = new PersonalityVote(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new[] { PersonalityTraitIds.Creative, PersonalityTraitIds.Intelligent, PersonalityTraitIds.Calm });
        context.PersonalityVotes.Add(vote);
        context.ChangeTracker.DetectChanges();

        Assert.Equal(EntityState.Added, context.Entry(vote).State);
        Assert.All(context.ChangeTracker.Entries<PersonalityVoteTrait>(), entry => Assert.Equal(EntityState.Added, entry.State));
    }
}
