using static Yakku.Tests.PersonalityTestIds;
using Yakku.Application.Personality;
using Yakku.Application.Personality.DTOs;
using Yakku.Application.Personality.Validators;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

/// <summary>Personality card rules that need no database: lifecycle, request validation and the results calculation.</summary>
public sealed class PersonalityUnitTests
{
    // ---- registration ------------------------------------------------------------------------------------------

    [Fact]
    public void ANewUser_GetsExactlyOneDraftCard()
    {
        var user = new User("+910000000001", "Quiet Fox");

        var card = user.PersonalityCard;
        Assert.NotNull(card);
        Assert.Equal(user.Id, card.UserId);
        Assert.Equal(PersonalityCardStatus.Draft, card.Status);
        Assert.Equal(5, card.RequiredVotes);
        Assert.Equal(0, card.TotalVotes);
        Assert.Null(card.CompletedAt);
    }

    [Fact]
    public void ANewCard_GetsASixDigitHexShareCode_ThatDiffersBetweenCards()
    {
        var codes = Enumerable.Range(0, 50).Select(i => new User($"+91000001{i:D4}", $"Fox {i}").PersonalityCard.ShareCode).ToList();

        Assert.All(codes, code => Assert.Matches("^[0-9a-f]{6}$", code));
        Assert.True(codes.Distinct().Count() > 45);
    }

    // ---- lifecycle ---------------------------------------------------------------------------------------------

    [Fact]
    public void SelfAssessment_MovesDraftToCollecting_AndCannotBeRepeated()
    {
        var card = new PersonalityCard(Guid.NewGuid());

        card.SubmitSelfAssessment(new[] { 1, 2, 3 }.Select(T).ToArray());

        Assert.Equal(PersonalityCardStatus.Collecting, card.Status);
        Assert.Equal(3, card.SelfTraits.Count);
        Assert.Throws<InvalidOperationException>(() => card.SubmitSelfAssessment(new[] { 4, 5, 6 }.Select(T).ToArray()));
        Assert.Equal(3, card.SelfTraits.Count);
    }

    [Fact]
    public void ADraftCard_RejectsVotes()
    {
        var card = new PersonalityCard(Guid.NewGuid());

        Assert.Throws<InvalidOperationException>(() => card.RegisterVote());
        Assert.Equal(0, card.TotalVotes);
    }

    [Fact]
    public void FourVotes_KeepCollecting_FifthCompletes_SixthIsRejected()
    {
        var card = new PersonalityCard(Guid.NewGuid());
        card.SubmitSelfAssessment(new[] { 1, 2, 3 }.Select(T).ToArray());

        for (var i = 0; i < 4; i++)
        {
            card.RegisterVote();
        }

        Assert.Equal(PersonalityCardStatus.Collecting, card.Status);
        Assert.Null(card.CompletedAt);

        card.RegisterVote();

        Assert.Equal(PersonalityCardStatus.Completed, card.Status);
        Assert.Equal(5, card.TotalVotes);
        Assert.NotNull(card.CompletedAt);
        Assert.Throws<InvalidOperationException>(() => card.RegisterVote());
        Assert.Equal(5, card.TotalVotes);
    }

    // ---- validation --------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(new int[] { 1, 2, 3 }, true)]
    [InlineData(new int[] { 1, 2 }, false)]
    [InlineData(new int[] { 1, 2, 3, 4 }, false)]
    [InlineData(new int[] { }, false)]
    [InlineData(new int[] { 1, 1, 2 }, false)]
    public void Validator_RequiresExactlyThreeDistinctIds(int[] ids, bool valid)
    {
        var result = new SubmitTraitsValidator().Validate(new SubmitTraitsRequest { TraitIds = ids.Select(T).ToList() });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Validator_RejectsAnEmptyGuid()
    {
        var request = new SubmitTraitsRequest { TraitIds = new List<Guid> { Guid.Empty, T(1), T(2) } };

        Assert.False(new SubmitTraitsValidator().Validate(request).IsValid);
    }

    [Fact]
    public void Validator_RejectsMissingList()
    {
        var result = new SubmitTraitsValidator().Validate(new SubmitTraitsRequest { TraitIds = null! });

        Assert.False(result.IsValid);
    }

    // ---- results -----------------------------------------------------------------------------------------------

    private static SelfTraitRef Self(int id, int order) => new(T(id), $"T{id}", order);

    private static TraitTally Tally(int id, int order, int votes) => new(T(id), $"T{id}", order, votes);

    [Fact]
    public void Compare_SplitsCommonSelfOnlyAndCrowdOnly()
    {
        // The spec example: self = Confident(1), Creative(3), Ambitious(5); crowd top = Confident, Reliable(10), Creative.
        var self = new[] { Self(1, 1), Self(3, 3), Self(5, 5) };
        var tallies = new[]
        {
            Tally(1, 1, 4), Tally(10, 10, 4), Tally(3, 3, 3), Tally(6, 6, 2), Tally(8, 8, 1), Tally(5, 5, 1)
        };

        var result = PersonalityResultCalculator.Compare(self, tallies);

        Assert.Equal(new[] { 1, 10, 3 }, result.CrowdTop.Select(t => N(t.TraitId)));
        Assert.Equal(new[] { 1, 3 }, result.Common.Select(t => N(t.TraitId)));
        Assert.Equal(new[] { 5 }, result.SelfOnly.Select(t => N(t.TraitId)));
        Assert.Equal(new[] { 10 }, result.CrowdOnly.Select(t => N(t.TraitId)));
        Assert.Equal(new[] { 1, 3, 5 }, result.SelfTraits.Select(t => N(t.TraitId)));
    }

    [Fact]
    public void Compare_BreaksTiesByDisplayOrderThenId_RegardlessOfInputOrder()
    {
        // Four traits tied on 2 votes: only three fit, the lowest display orders win.
        var tallies = new[] { Tally(9, 9, 2), Tally(2, 2, 2), Tally(7, 7, 2), Tally(4, 4, 2) };
        var self = new[] { Self(1, 1), Self(2, 2), Self(3, 3) };

        var forward = PersonalityResultCalculator.Compare(self, tallies);
        var reversed = PersonalityResultCalculator.Compare(self, tallies.Reverse());

        Assert.Equal(new[] { 2, 4, 7 }, forward.CrowdTop.Select(t => N(t.TraitId)));
        Assert.Equal(forward.CrowdTop.Select(t => N(t.TraitId)), reversed.CrowdTop.Select(t => N(t.TraitId)));
    }

    [Fact]
    public void Compare_WithIdenticalSelectionsOnBothSides_HasNoDifferences()
    {
        var self = new[] { Self(1, 1), Self(2, 2), Self(3, 3) };
        var tallies = new[] { Tally(1, 1, 5), Tally(2, 2, 5), Tally(3, 3, 5) };

        var result = PersonalityResultCalculator.Compare(self, tallies);

        Assert.Equal(3, result.Common.Count);
        Assert.Empty(result.SelfOnly);
        Assert.Empty(result.CrowdOnly);
    }
}
