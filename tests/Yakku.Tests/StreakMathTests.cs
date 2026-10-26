using Yakku.Application.Streaks;

namespace Yakku.Tests;

public class StreakMathTests
{
    private static readonly DateOnly Day = new(2026, 3, 10);

    [Fact]
    public void FirstActivity_StartsAStreakOfOne()
    {
        var step = StreakMath.Next(null, Day);

        Assert.Equal(StreakTransition.First, step.Transition);
        Assert.Equal(new StreakState(1, 1, Day), step.State);
    }

    [Fact]
    public void SameDay_DoesNotChangeAnything()
    {
        var state = new StreakState(4, 7, Day);

        var step = StreakMath.Next(state, Day);

        Assert.Equal(StreakTransition.SameDay, step.Transition);
        Assert.Equal(state, step.State);
    }

    [Fact]
    public void TheNextDay_IncrementsTheStreak_AndRaisesTheRecordOnlyWhenBeaten()
    {
        var below = StreakMath.Next(new StreakState(2, 5, Day), Day.AddDays(1));
        var equal = StreakMath.Next(new StreakState(4, 5, Day), Day.AddDays(1));
        var beaten = StreakMath.Next(new StreakState(5, 5, Day), Day.AddDays(1));

        Assert.Equal(new StreakState(3, 5, Day.AddDays(1)), below.State);
        Assert.Equal(new StreakState(5, 5, Day.AddDays(1)), equal.State);
        Assert.Equal(new StreakState(6, 6, Day.AddDays(1)), beaten.State);
        Assert.All([below, equal, beaten], step => Assert.Equal(StreakTransition.Consecutive, step.Transition));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(400)]
    public void AfterAMissedDay_TheStreakRestartsAtOne_AndTheRecordSurvives(int daysLater)
    {
        var step = StreakMath.Next(new StreakState(9, 9, Day), Day.AddDays(daysLater));

        Assert.Equal(StreakTransition.Gap, step.Transition);
        Assert.Equal(new StreakState(1, 9, Day.AddDays(daysLater)), step.State);
    }

    [Fact]
    public void AnEarlierDay_NeverChangesTheStoredSummary()
    {
        var state = new StreakState(3, 8, Day);

        var step = StreakMath.Next(state, Day.AddDays(-5));

        Assert.Equal(StreakTransition.Historical, step.Transition);
        Assert.Equal(state, step.State);
    }

    [Theory]
    [InlineData(0, 6)] // active today
    [InlineData(1, 6)] // last active yesterday: still alive, today can extend it
    [InlineData(2, 0)] // a whole day missed: over
    [InlineData(30, 0)]
    public void TheEffectiveStreak_ExpiresAfterAFullMissedDay(int daysSinceLastActivity, int expected)
    {
        var today = new DateOnly(2026, 6, 15);
        var state = new StreakState(6, 10, today.AddDays(-daysSinceLastActivity));

        Assert.Equal(expected, StreakMath.EffectiveCurrent(state, today));
    }

    [Fact]
    public void AUserWhoWasNeverActive_HasNoStreak()
    {
        Assert.Equal(0, StreakMath.EffectiveCurrent(null, Day));
    }
}

public class StreakCalendarTests
{
    [Fact]
    public void TheDayChangesExactlyAtUtcMidnight()
    {
        var lastTick = new DateTime(2026, 3, 10, 23, 59, 59, DateTimeKind.Utc).AddTicks(9_999_999);
        var midnight = new DateTime(2026, 3, 11, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(2026, 3, 10), StreakCalendar.ToActivityDate(lastTick));
        Assert.Equal(new DateOnly(2026, 3, 11), StreakCalendar.ToActivityDate(midnight));
    }

    [Fact]
    public void ALocalTimestamp_IsConvertedToUtcBeforeTheDateIsTaken()
    {
        var utc = new DateTime(2026, 3, 10, 23, 30, 0, DateTimeKind.Utc);

        Assert.Equal(StreakCalendar.ToActivityDate(utc), StreakCalendar.ToActivityDate(utc.ToLocalTime()));
    }

    [Theory]
    [InlineData("2026-03-08T06:59:59Z", "2026-03-08")] // just before the US spring-forward instant
    [InlineData("2026-03-08T07:00:00Z", "2026-03-08")] // just after: the UTC day is unaffected
    [InlineData("2026-11-01T05:59:59Z", "2026-11-01")] // US fall-back
    [InlineData("2026-11-01T06:00:00Z", "2026-11-01")]
    public void DaylightSavingTransitions_DoNotAffectUtcDays(string instant, string expectedDate)
    {
        var timestamp = DateTime.Parse(instant, null, System.Globalization.DateTimeStyles.AdjustToUniversal);

        Assert.Equal(DateOnly.Parse(expectedDate), StreakCalendar.ToActivityDate(timestamp));
    }
}
