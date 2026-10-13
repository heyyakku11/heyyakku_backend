using Yakku.Domain.Entities;

namespace Yakku.Application.Votes.Interfaces
{
    public interface IVoteRepository
    {
        Task AddAsync(PollVote vote, CancellationToken cancellationToken = default);

        Task<bool> ExistsForUserAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default);

        Task<PollVote?> GetByUserAndPollAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
