using Yakku.Application.Polls;
using Yakku.Application.Sharing;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class SharedResultTests
{
    private static readonly Guid A = Guid.NewGuid();
    private static readonly Guid B = Guid.NewGuid();

    private static VotableContent Content(bool open, DateTime? expiresAt = null) => new()
    {
        ContentType = ShareContentType.Poll,
        ContentId = Guid.NewGuid(),
        ShareCode = "abc12345",
        OwnerId = Guid.NewGuid(),
        Question = "Pick one",
        Status = open ? "ACTIVE" : "CLOSED",
        IsOpen = open,
        PubliclyReadable = false,
        ExpiresAt = expiresAt,
        CreatedAt = DateTime.UtcNow.AddDays(-2),
        Options = [new VotableOption(A, "Tea", 1), new VotableOption(B, "Coffee", 2)]
    };

    private static PollVoteCountSnapshot Tally(int a, int b, int other) => new(
        a + b,
        0,
        0,
        new Dictionary<Guid, int> { [A] = a, [B] = b },
        new Dictionary<Guid, int>(),
        other);

    [Fact]
    public void An_ended_card_with_enough_votes_shows_percentages_and_the_leader()
    {
        var result = SharedContentMapper.ToResult(Content(open: false), Tally(3, 1, other: 4), DateTime.UtcNow);

        Assert.True(result.ResultsAvailable);
        Assert.False(result.IsLive);
        Assert.Equal(75m, result.Options.Single(o => o.Text == "Tea").Percentage);
        Assert.True(result.Options.Single(o => o.Text == "Tea").IsLeading);
        Assert.False(result.Options.Single(o => o.Text == "Coffee").IsLeading);
    }

    [Fact]
    public void A_live_card_sends_no_options()
    {
        var result = SharedContentMapper.ToResult(Content(open: true), Tally(3, 1, other: 4), DateTime.UtcNow);

        Assert.False(result.ResultsAvailable);
        Assert.True(result.IsLive);
        Assert.Empty(result.Options);
    }

    [Fact]
    public void An_expired_card_counts_as_ended()
    {
        var result = SharedContentMapper.ToResult(
            Content(open: true, expiresAt: DateTime.UtcNow.AddHours(-1)),
            Tally(2, 2, other: 4),
            DateTime.UtcNow);

        Assert.True(result.ResultsAvailable);
        Assert.False(result.IsLive);
    }

    [Fact]
    public void An_ended_card_under_the_vote_limit_stays_locked()
    {
        var result = SharedContentMapper.ToResult(Content(open: false), Tally(1, 0, other: 1), DateTime.UtcNow);

        Assert.False(result.ResultsAvailable);
        Assert.False(result.IsLive);
        Assert.Empty(result.Options);
    }
}
