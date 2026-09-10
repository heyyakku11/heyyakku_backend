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
    }
}
