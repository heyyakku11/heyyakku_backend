using Yakku.Domain.Entities;

namespace Yakku.Application.Community.Interfaces
{
    public interface ICommunityQueuedQuestionRepository
    {
        Task<int> CountByUserAsync(Guid userId, CancellationToken cancellationToken = default);

        Task<List<CommunityQueuedQuestion>> ListByUserAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<CommunityQueuedQuestion?> GetByIdForUserAsync(
            Guid id,
            Guid userId,
            CancellationToken cancellationToken = default);

        void Add(CommunityQueuedQuestion question);

        void Remove(CommunityQueuedQuestion question);

        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
