using Microsoft.EntityFrameworkCore;
using Yakku.Application.Images.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class ImageRepository : IImageRepository
    {
        private readonly YakkuDbContext _context;

        public ImageRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(Image image, CancellationToken cancellationToken = default)
        {
            await _context.Images.AddAsync(image, cancellationToken);
        }

        public async Task<Image?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _context.Images
                .FirstOrDefaultAsync(image => image.Id == id, cancellationToken);
        }

        public async Task<IReadOnlyCollection<Guid>> GetExistingIdsAsync(
            IReadOnlyCollection<Guid> ids,
            CancellationToken cancellationToken = default)
        {
            if (ids.Count == 0)
            {
                return [];
            }

            return await _context.Images
                .AsNoTracking()
                .Where(image => ids.Contains(image.Id))
                .Select(image => image.Id)
                .ToListAsync(cancellationToken);
        }

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
