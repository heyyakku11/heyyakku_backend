using Yakku.Application.Polls.DTOs;

namespace Yakku.Application.Polls.Interfaces
{
    public interface IMediaUploadSessionService
    {
        Task<MediaUploadSessionResponse> CreateAsync(
            CreateMediaUploadSessionRequest request,
            MediaOwner owner,
            CancellationToken cancellationToken = default);

        Task<MediaUploadSessionResponse> CompleteAsync(
            Guid sessionId,
            MediaOwner owner,
            CompleteMediaUploadRequest request,
            CancellationToken cancellationToken = default);

        Task<MediaUploadSessionResponse> GetAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default);

        Task<MediaAccessResponse> GetAccessAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default);

        Task CancelAsync(
            Guid sessionId,
            MediaOwner owner,
            CancellationToken cancellationToken = default);
    }
}
