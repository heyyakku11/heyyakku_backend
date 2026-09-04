using PollEntity = Yakku.Domain.Entities.Polls;

namespace Yakku.Application.Polls.Interfaces
{
    public interface IPollRepository
    {
        Task AddAsync(PollEntity poll, CancellationToken cancellationToken = default);
        Task<PollEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<PollEntity>> GetCreatedByUserAsync(
            Guid userId,
            DateTime? cursorCreatedAt,
            Guid? cursorId,
            int take,
            CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
