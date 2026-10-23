using Yakku.Domain.Enums;
using PollEntity = Yakku.Domain.Entities.Poll;

namespace Yakku.Application.Polls.Interfaces
{
    public interface IPollRepository
    {
        Task AddAsync(PollEntity poll, CancellationToken cancellationToken = default);

        void Delete(PollEntity poll);

        Task<PollEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task<PollEntity?> GetByIdAndCreatorAsync(
            Guid id,
            Guid creatorId,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<PollEntity>> GetCreatedByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default);

        Task<int> TryMarkActiveAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default);

        Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<AnsweredPollEntry>> GetAnsweredByUserAsync(
            Guid userId,
            DateTime? cursorVotedAt,
            Guid? cursorPollId,
            int take,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyDictionary<Guid, PollVoteCountSnapshot>> GetVoteCountsAsync(
            IReadOnlyCollection<Guid> pollIds,
            CancellationToken cancellationToken = default);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
