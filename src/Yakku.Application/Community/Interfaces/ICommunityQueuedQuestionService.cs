using Yakku.Application.Community.DTOs;

namespace Yakku.Application.Community.Interfaces
{
    public interface ICommunityQueuedQuestionService
    {
        Task<IReadOnlyList<CommunityQueuedQuestionResponse>> ListAsync(
            Guid userId,
            CancellationToken cancellationToken = default);

        Task<CommunityQueuedQuestionResponse> CreateAsync(
            Guid userId,
            UpsertCommunityQueuedQuestionRequest request,
            CancellationToken cancellationToken = default);

        Task<CommunityQueuedQuestionResponse> UpdateAsync(
            Guid userId,
            Guid id,
            UpsertCommunityQueuedQuestionRequest request,
            CancellationToken cancellationToken = default);

        Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken = default);
    }
}
