using Yakku.Domain.Entities;

namespace Yakku.Application.Categories.Interfaces
{
    public interface ICategoryRepository
    {
        Task<Category?> GetActiveByIdAsync(Guid id, CancellationToken cancellationToken = default);
    }
}
