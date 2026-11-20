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

    /// <summary>When true, the next <see cref="TryAddAsync"/> reports an idempotency conflict.</summary>
    public bool ForceIdempotencyConflict { get; set; }

    /// <summary>
    /// How many <see cref="FindByIdempotencyKeyAsync"/> calls should miss so a race after insert can be simulated.
    /// </summary>
    public int SuppressIdempotencyLookupCount { get; set; }

    public Task<Guest?> TryAddAsync(Guest guest, CancellationToken cancellationToken = default)
    {
        if (ForceIdempotencyConflict)
        {
            ForceIdempotencyConflict = false;
            return Task.FromResult<Guest?>(null);
        }

        if (guest.IdempotencyKey is not null
            && _guests.Any(existing => existing.IdempotencyKey == guest.IdempotencyKey))
        {
            return Task.FromResult<Guest?>(null);
        }

        _guests.Add(guest);
        return Task.FromResult<Guest?>(guest);
    }

    public Task<Guest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(_guests.FirstOrDefault(guest => guest.Id == id));

    public Task<Guest?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_guests.FirstOrDefault(guest => guest.SessionTokenHash == tokenHash));
    }

    public Task<Guest?> FindByIdempotencyKeyAsync(
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        if (SuppressIdempotencyLookupCount > 0)
        {
            SuppressIdempotencyLookupCount--;
            return Task.FromResult<Guest?>(null);
        }

        return Task.FromResult(
            _guests.FirstOrDefault(guest => guest.IdempotencyKey == idempotencyKey));
    }

    public Task<Guest?> GetByTokenHashForUpdateAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        return GetByTokenHashAsync(tokenHash, cancellationToken);
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
