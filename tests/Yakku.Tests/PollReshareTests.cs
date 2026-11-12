using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

/// <summary>Resharing: a closed poll opens again for 24 more hours, on the same share link.</summary>
public class PollReshareTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 10, 0, 0, DateTimeKind.Utc);

    private static Poll ActivePoll(DateTime? expiresAt)
    {
        var poll = new Poll(Guid.NewGuid(), "Which?", PollContentType.Question, expiresAt);
        poll.AddTextOption("A", 1);
        poll.AddTextOption("B", 2);
        poll.Publish();
        return poll;
    }

    [Fact]
    public void ClosedByTheOwner_ReopensWithItsExpiryMovedBy24Hours()
    {
        var poll = ActivePoll(Now.AddDays(3));
        poll.TryClose();

        Assert.True(poll.TryReshare(Now));

        Assert.Equal(PollStatus.Active, poll.Status);
        Assert.Equal(Now.AddDays(3).AddHours(24), poll.ExpiresAt);
        Assert.True(poll.IsAcceptingVotes(Now));
    }

    [Fact]
    public void ClosedPollWithoutAnExpiry_GetsExactly24HoursFromNow()
    {
        var poll = ActivePoll(null);
        poll.TryClose();

        Assert.True(poll.TryReshare(Now));

        Assert.Equal(Now.AddHours(24), poll.ExpiresAt);
    }

    [Fact]
    public void PollWhoseTimeRanOut_IsClosed_AndReopensFor24HoursFromNow()
    {
        var poll = ActivePoll(Now.AddMinutes(1));
        var later = Now.AddDays(2);
        Assert.False(poll.IsAcceptingVotes(later));
        Assert.True(poll.IsClosed(later));

        Assert.True(poll.TryReshare(later));

        // Not 24 hours after the old, already passed expiry: that would still be in the past.
        Assert.Equal(later.AddHours(24), poll.ExpiresAt);
        Assert.True(poll.IsAcceptingVotes(later));
    }

    [Fact]
    public void AnOpenPoll_CannotBeReshared_AndIsLeftUntouched()
    {
        var expiry = Now.AddDays(1);
        var poll = ActivePoll(expiry);

        Assert.False(poll.IsClosed(Now));
        Assert.False(poll.TryReshare(Now));

        Assert.Equal(expiry, poll.ExpiresAt);
        Assert.Equal(PollStatus.Active, poll.Status);
    }

    [Fact]
    public void ADraft_CannotBeReshared()
    {
        var poll = new Poll(Guid.NewGuid(), "Which?", PollContentType.Question, Now.AddDays(1));

        Assert.False(poll.TryReshare(Now));
        Assert.Equal(PollStatus.Draft, poll.Status);
    }

    [Fact]
    public void ReshareKeepsTheShareCode_SoTheSameLinkWorks()
    {
        var poll = ActivePoll(null);
        var code = poll.ShareCode;
        poll.TryClose();

        poll.TryReshare(Now);

        Assert.Equal(code, poll.ShareCode);
    }

    [Fact]
    public void ResharingTwice_AddsAnother24Hours_OnlyAfterItIsClosedAgain()
    {
        var poll = ActivePoll(null);
        poll.TryClose();
        poll.TryReshare(Now);

        Assert.False(poll.TryReshare(Now.AddHours(1)));

        poll.TryClose();
        Assert.True(poll.TryReshare(Now.AddHours(2)));
        Assert.Equal(Now.AddHours(24).AddHours(24), poll.ExpiresAt);
    }
}
