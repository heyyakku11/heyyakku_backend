using Yakku.Domain.Entities;

namespace Yakku.Application.Categories.Interfaces
{
    public interface ICategoryRepository
    {
        Task<Category?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
        Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken = default);
        Task<bool> ExistsBySlugAsync(string slug, CancellationToken cancellationToken = default);
        Task AddAsync(Category category, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
