using Microsoft.EntityFrameworkCore;
using Yakku.Application.Categories.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        private readonly YakkuDbContext _context;

        public CategoryRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<Category?> GetActiveByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AsNoTracking()
                .FirstOrDefaultAsync(
                    category => category.Id == id && category.IsActive,
                    cancellationToken);
        }

        public async Task<bool> ExistsByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            var normalized = name.Trim().ToLower();
            return await _context.Categories
                .AnyAsync(category => category.Name.ToLower() == normalized, cancellationToken);
        }

        public async Task<bool> ExistsBySlugAsync(
            string slug,
            CancellationToken cancellationToken = default)
        {
            return await _context.Categories
                .AnyAsync(category => category.Slug == slug, cancellationToken);
        }

        public async Task AddAsync(Category category, CancellationToken cancellationToken = default)
        {
            await _context.Categories.AddAsync(category, cancellationToken);
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return _context.SaveChangesAsync(cancellationToken);
        }
    }
}
