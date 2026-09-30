using Yakku.Application.Categories.DTOs;

namespace Yakku.Application.Categories.Interfaces
{
    public interface ICategoryService
    {
        Task<CategoryResponse> CreateAsync(
            CreateCategoryRequest request,
            CancellationToken cancellationToken = default);

        Task<IReadOnlyList<CategoryResponse>> ListAsync(
            CancellationToken cancellationToken = default);
    }
}
