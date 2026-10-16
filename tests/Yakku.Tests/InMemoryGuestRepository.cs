using Yakku.Application.Guests.Interfaces;
using Yakku.Domain.Entities;

namespace Yakku.Tests;

internal sealed class InMemoryGuestRepository : IGuestRepository
{
    private readonly List<Guest> _guests = [];

    public IReadOnlyList<Guest> All => _guests;

    /// <summary>Makes the next save fail, to exercise failure handling around the claim.</summary>
    public bool ThrowOnSave { get; set; }

    public void Seed(Guest guest) => _guests.Add(guest);

    public Task AddAsync(Guest guest, CancellationToken cancellationToken = default)
    {
        _guests.Add(guest);
        return Task.CompletedTask;
    }

    public Task<Guest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_guests.FirstOrDefault(guest => guest.Id == id));

    public Task<Guest?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_guests.FirstOrDefault(guest => guest.GuestTokenHash == tokenHash));
    }

    public Task<Guest?> GetByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return GetByTokenHashAsync(tokenHash, cancellationToken);
    }

    public Task<IReadOnlyList<Guest>> ListAsync(
        bool? active,
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<Guest> query = _guests;
        if (active == true)
        {
            query = query.Where(guest => guest.ExpiresAt > utcNow);
        }
        else if (active == false)
        {
            query = query.Where(guest => guest.ExpiresAt <= utcNow);
        }

        return Task.FromResult<IReadOnlyList<Guest>>(query.ToList());
    }

    public Task<IReadOnlyList<Guid>> ListExpiredIdsAsync(
        DateTime expiredBeforeUtc,
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Guid> ids = _guests
            .Where(guest => guest.ExpiresAt <= expiredBeforeUtc)
            .OrderBy(guest => guest.ExpiresAt)
            .Take(batchSize)
            .Select(guest => guest.Id)
            .ToList();
        return Task.FromResult(ids);
    }

    public Task<bool> DeleteExpiredByIdAsync(
        Guid guestId,
        DateTime expiredBeforeUtc,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_guests.RemoveAll(guest => guest.Id == guestId && guest.ExpiresAt <= expiredBeforeUtc) == 1);
    }

    public void Delete(Guest guest) => _guests.Remove(guest);

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken = default)
    {
        var snapshot = _guests.ToList();
        try
        {
            await action(cancellationToken);
        }
        catch
        {
            _guests.Clear();
            _guests.AddRange(snapshot);
            throw;
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (ThrowOnSave)
        {
            throw new InvalidOperationException("Simulated database failure.");
        }

        return Task.CompletedTask;
    }
}
