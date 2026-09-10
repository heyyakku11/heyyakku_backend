using Yakku.Domain.Entities;

namespace Yakku.Application.Images.Interfaces
{
    public interface IImageRepository
    {
        Task AddAsync(Image image, CancellationToken cancellationToken = default);
        Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<Guid>> GetExistingIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
