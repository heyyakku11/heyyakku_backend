using Yakku.Domain.Entities;

namespace Yakku.Application.Polls.Interfaces
{
    public interface ISavedPollRepository
    {
        Task<SavedPoll?> GetByUserAndPollAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default);

        Task AddAsync(SavedPoll savedPoll, CancellationToken cancellationToken = default);

        Task DeleteAsync(SavedPoll savedPoll, CancellationToken cancellationToken = default);

        Task<bool> SaveChangesAsync(CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SavedPoll>> ListByUserAsync(
            Guid userId,
            DateTime? cursorSavedAt,
            Guid? cursorPollId,
            int take,
            CancellationToken cancellationToken = default);
    }
}
