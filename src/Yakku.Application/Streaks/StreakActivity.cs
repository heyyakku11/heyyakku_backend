namespace Yakku.Application.Streaks
{
    /// <summary>What a user did. Add a value here and to <see cref="StreakRules"/> to make a new action count.</summary>
    public enum StreakActivity
    {
        PollCreated = 1,
        VoteCast = 2
    }

    /// <summary>The single place that decides which activities earn streak credit.</summary>
    public static class StreakRules
    {
        public static bool Qualifies(StreakActivity activity)
        {
            return activity is StreakActivity.PollCreated or StreakActivity.VoteCast;
        }
    }
}
