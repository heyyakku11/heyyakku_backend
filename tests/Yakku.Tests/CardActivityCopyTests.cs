using Yakku.Application.PushNotifications;

namespace Yakku.Tests;

public class CardActivityCopyTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Progress_SaysHowManyVotesAreStillNeeded_AndWhenItCloses()
    {
        var (title, body) = CardActivityCopy.ForVote(
            "If I told you a secret, how long before someone else knows?",
            friendVotes: 2,
            threshold: 3,
            expiresAt: Now.AddHours(2),
            Now);

        Assert.Equal("2 of 3 votes", title);
        Assert.Equal(
            "One more vote on \"If I told you a secret, how long before someone else knows?\" Closes in 2h.",
            body);
    }

    [Fact]
    public void Progress_UsesPluralVotes_AndLeavesOutTheCloseTime_WhenTheCardHasNoEnd()
    {
        var (title, body) = CardActivityCopy.ForVote(
            "Which firecracker am I?",
            friendVotes: 1,
            threshold: 3,
            expiresAt: null,
            Now);

        Assert.Equal("1 of 3 votes", title);
        Assert.Equal("2 more votes on \"Which firecracker am I?\".", body);
    }

    [Fact]
    public void ResultReady_UsesTheSameLine_ForAPollAFunCardAndACheck()
    {
        var (title, body) = CardActivityCopy.ForVote(
            "Do people like me, or just like having me around?",
            friendVotes: 7,
            threshold: 3,
            expiresAt: Now.AddHours(5),
            Now);

        Assert.Equal("Your result is ready", title);
        Assert.Equal(
            "7 friends answered \"Do people like me, or just like having me around?\" See if they agree with you.",
            body);
    }

    [Fact]
    public void ResultReady_UsesFriend_WhenOnlyOnePersonAnswered()
    {
        var (_, body) = CardActivityCopy.ForVote("Am I chill?", 1, 1, null, Now);

        Assert.StartsWith("1 friend answered", body);
    }

    [Fact]
    public void Closed_PointsAtTheFinalResult_WhenEnoughFriendsAnswered()
    {
        var (title, body) = CardActivityCopy.ForClosed("Am I chill or chaotic?", friendVotes: 4, threshold: 3);

        Assert.Equal("Closed", title);
        Assert.Equal("\"Am I chill or chaotic?\" view final result.", body);
    }

    [Fact]
    public void Closed_SaysThereIsNoResult_WhenTheThresholdWasNotMet()
    {
        var (title, body) = CardActivityCopy.ForClosed("Am I chill or chaotic?", friendVotes: 2, threshold: 3);

        Assert.Equal("Closed", title);
        Assert.Equal(
            "\"Am I chill or chaotic?\" closed with 2 votes. Not enough to show a result.",
            body);
    }

    [Fact]
    public void Personality_SaysTheTopThreeAreReady()
    {
        var (title, body) = CardActivityCopy.ForPersonality(5);

        Assert.Equal("Who you are is in", title);
        Assert.Equal("5 friends described you. Your top 3 are ready.", body);
    }

    [Theory]
    [InlineData(2, "2h")]
    [InlineData(26, "1d")]
    [InlineData(0.5, "30m")]
    public void TimeLeft_MatchesTheShortInboxClock(double hours, string expected)
    {
        Assert.Equal(expected, CardActivityCopy.TimeLeft(Now.AddHours(hours), Now));
    }
}
