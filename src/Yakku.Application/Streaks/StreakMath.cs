namespace Yakku.Application.Streaks
{
    public readonly record struct StreakState(int Current, int Longest, DateOnly LastActivityDate);

    public enum StreakTransition
    {
        /// <summary>No previous activity: the streak starts at one.</summary>
        First,

        /// <summary>The day is already the last counted day: nothing changes.</summary>
        SameDay,

        /// <summary>The day after the last counted day: the streak grows by one.</summary>
        Consecutive,

        /// <summary>One or more whole days were missed: the streak restarts at one.</summary>
        Gap,

        /// <summary>
        /// A day before the last counted day (late or out-of-order). The stored summary is not changed by it; the
        /// caller rebuilds the summary from the day history instead.
        /// </summary>
        Historical
    }

    public readonly record struct StreakStep(StreakTransition Transition, StreakState State);

    /// <summary>The streak arithmetic, in one place and free of I/O.</summary>
    public static class StreakMath
    {
        public static StreakStep Next(StreakState? current, DateOnly date)
        {
            if (current is not { } state)
            {
                return new StreakStep(StreakTransition.First, new StreakState(1, 1, date));
            }

            if (date == state.LastActivityDate)
            {
                return new StreakStep(StreakTransition.SameDay, state);
            }

            if (date < state.LastActivityDate)
            {
                return new StreakStep(StreakTransition.Historical, state);
            }

            var consecutive = date == state.LastActivityDate.AddDays(1);
            var streak = consecutive ? state.Current + 1 : 1;
            return new StreakStep(
                consecutive ? StreakTransition.Consecutive : StreakTransition.Gap,
                new StreakState(streak, Math.Max(state.Longest, streak), date));
        }

        /// <summary>
        /// The streak as it should be shown on <paramref name="today"/>: still running if the last activity was today
        /// or yesterday, otherwise over (zero). The longest streak never changes.
        /// </summary>
        public static int EffectiveCurrent(StreakState? state, DateOnly today)
        {
            return state is { } s && s.LastActivityDate >= today.AddDays(-1) ? s.Current : 0;
        }
    }
}
