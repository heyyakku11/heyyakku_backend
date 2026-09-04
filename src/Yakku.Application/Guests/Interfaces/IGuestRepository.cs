using Yakku.Domain.Entities;

namespace Yakku.Application.Guests.Interfaces
{
    public interface IGuestRepository
    {
        Task AddAsync(Guest guest, CancellationToken cancellationToken = default);
        Task<Guest?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default);
        Task SaveChangesAsync(CancellationToken cancellationToken = default);
    }
}
