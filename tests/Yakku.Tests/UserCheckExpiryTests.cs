using Yakku.Domain.Entities;

namespace Yakku.Tests;

/// <summary>Cards made from a fun card live for 24 hours, can be closed, and can be reshared for 24 more.</summary>
public class UserCheckExpiryTests
{
    private static readonly DateTime Now = new(2026, 11, 13, 10, 0, 0, DateTimeKind.Utc);

    private static UserCheck FunCard(DateTime? expiresAt) =>
        UserCheck.Create(Guid.NewGuid(), Guid.NewGuid(), "code123", expiresAt);

    [Fact]
    public void AFunCardIsOpenUntilItsExpiry_ThenClosed()
    {
        var card = FunCard(Now.AddHours(24));

        Assert.False(card.IsClosedAt(Now.AddHours(23)));
        Assert.True(card.IsClosedAt(Now.AddHours(24)));
        Assert.True(card.IsClosedAt(Now.AddHours(25)));
    }

    [Fact]
    public void AMadeWeeklyCheck_HasNoExpiry_SoItStaysOpenUntilTheOwnerCloses()
    {
        var card = FunCard(null);

        Assert.Null(card.ExpiresAt);
        Assert.False(card.IsClosedAt(Now.AddYears(5)));
        Assert.True(card.TryClose());
        Assert.True(card.IsClosedAt(Now));
    }

    [Fact]
    public void TheOwnerCanCloseAFunCardEarly()
    {
        var card = FunCard(Now.AddHours(24));

        Assert.True(card.TryClose());

        Assert.True(card.IsClosedAt(Now));
    }

    [Fact]
    public void ResharingAClosedFunCard_ReopensItAndAdds24Hours()
    {
        var expiry = Now.AddHours(10);
        var card = FunCard(expiry);
        card.TryClose();

        Assert.True(card.TryReshare(Now));

        Assert.Null(card.ClosedAt);
        Assert.Equal(expiry.AddHours(24), card.ExpiresAt);
        Assert.False(card.IsClosedAt(Now));
    }

    [Fact]
    public void ResharingAFunCardWhoseTimeRanOut_GivesAFull24HoursFromNow()
    {
        var card = FunCard(Now.AddHours(-3));
        Assert.True(card.IsClosedAt(Now));

        Assert.True(card.TryReshare(Now));

        // Not 24 hours after the old, already passed expiry: that would still be in the past.
        Assert.Equal(Now.AddHours(24), card.ExpiresAt);
        Assert.False(card.IsClosedAt(Now));
    }

    [Fact]
    public void AnOpenCard_CannotBeReshared_AndKeepsItsExpiry()
    {
        var expiry = Now.AddHours(5);
        var card = FunCard(expiry);

        Assert.False(card.TryReshare(Now));

        Assert.Equal(expiry, card.ExpiresAt);
    }

    [Fact]
    public void ResharingAClosedWeeklyCheck_JustReopensIt_WithoutAddingAnExpiry()
    {
        var card = FunCard(null);
        card.TryClose();

        Assert.True(card.TryReshare(Now));

        Assert.Null(card.ClosedAt);
        Assert.Null(card.ExpiresAt);
        Assert.False(card.IsClosedAt(Now.AddDays(30)));
    }

    [Fact]
    public void TheLifetime_IsExactly24Hours()
    {
        Assert.Equal(TimeSpan.FromHours(24), UserCheck.FunCardLifetime);
    }
}
