using Yakku.Domain.Entities;

namespace Yakku.Application.Votes.Interfaces
{
    public interface IVoteRepository
    {
        Task AddAsync(Vote vote, CancellationToken cancellationToken = default);
        Task<bool> ExistsAsync(Guid guestId, Guid pollId, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
