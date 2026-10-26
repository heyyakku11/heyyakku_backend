namespace Yakku.Application.Users.Interfaces
{
    public sealed record UserActivityCounts(int CardsMade, int VotesCast);

    public interface IUserStatsRepository
    {
        /// <summary>
        /// CardsMade: polls the user created (drafts excluded) plus checks the user made.
        /// VotesCast: votes the user cast as a signed-in user on other people's polls and checks; answering your
        /// own poll or check does not count.
        /// </summary>
        Task<UserActivityCounts> GetActivityCountsAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
