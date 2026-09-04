using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Guests.Services;
using Yakku.Domain.Entities;
using Xunit;

namespace Yakku.Application.Tests.Guests;

public class GuestIdentityServiceTests
{
    [Fact]
    public async Task Establish_NoToken_CreatesGuestAndReturnsRawToken()
    {
        var fixture = GuestFixture.Create();

        var result = await fixture.Service.EstablishAsync(null);

        Assert.Single(fixture.Guests.Items);
        Assert.Equal(fixture.Guests.Items[0].Id, result.GuestId);
        Assert.Equal("guest-token-1", result.RawTokenToSet);
        Assert.NotEqual(result.RawTokenToSet, fixture.Guests.Items[0].TokenHash);
        Assert.Equal(
            fixture.Hasher.Hash("guest-token-1"),
            fixture.Guests.Items[0].TokenHash);
    }

    [Fact]
    public async Task Establish_ValidToken_ReusesGuestAndDoesNotCreateAnother()
    {
        var fixture = GuestFixture.Create();
        var first = await fixture.Service.EstablishAsync(null);
        var createdAt = fixture.Guests.Items[0].CreatedAt;

        var second = await fixture.Service.EstablishAsync(first.RawTokenToSet);

        Assert.Single(fixture.Guests.Items);
        Assert.Equal(first.GuestId, second.GuestId);
        Assert.Null(second.RawTokenToSet);
        Assert.True(fixture.Guests.Items[0].LastSeenAt >= createdAt);
        Assert.Equal(2, fixture.Guests.SaveChangesCalls);
    }

    [Fact]
    public async Task Establish_UnknownToken_CreatesReplacementGuest()
    {
        var fixture = GuestFixture.Create();
        var first = await fixture.Service.EstablishAsync(null);

        var second = await fixture.Service.EstablishAsync("unknown-cookie-token");

        Assert.Equal(2, fixture.Guests.Items.Count);
        Assert.NotEqual(first.GuestId, second.GuestId);
        Assert.Equal("guest-token-2", second.RawTokenToSet);
        Assert.DoesNotContain(fixture.Guests.Items, guest => guest.TokenHash == "unknown-cookie-token");
        Assert.DoesNotContain(fixture.Guests.Items, guest => guest.TokenHash == second.RawTokenToSet);
    }

    [Fact]
    public async Task Establish_NeverStoresRawToken()
    {
        var fixture = GuestFixture.Create();
        var hasher = fixture.Hasher;

        var result = await fixture.Service.EstablishAsync(null);

        var stored = fixture.Guests.Items[0];
        Assert.NotEqual(result.RawTokenToSet, stored.TokenHash);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal(hasher.Hash(result.RawTokenToSet!), stored.TokenHash);
    }

    private sealed class GuestFixture
    {
        public required GuestIdentityService Service { get; init; }
        public required FakeGuestRepository Guests { get; init; }
        public required GuestTokenHasher Hasher { get; init; }

        public static GuestFixture Create()
        {
            var guests = new FakeGuestRepository();
            var hasher = new GuestTokenHasher();
            var service = new GuestIdentityService(
                guests,
                new SequentialTokenGenerator(),
                hasher);

            return new GuestFixture
            {
                Service = service,
                Guests = guests,
                Hasher = hasher
            };
        }
    }

    private sealed class SequentialTokenGenerator : IGuestTokenGenerator
    {
        private int _count;

        public string Generate()
        {
            return $"guest-token-{++_count}";
        }
    }

    internal sealed class FakeGuestRepository : IGuestRepository
    {
        public List<Guest> Items { get; } = [];
        public int SaveChangesCalls { get; private set; }

        public Task AddAsync(Guest guest, CancellationToken cancellationToken = default)
        {
            Items.Add(guest);
            return Task.CompletedTask;
        }

        public Task<Guest?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Items.FirstOrDefault(guest => guest.TokenHash == tokenHash));
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveChangesCalls++;
            return Task.CompletedTask;
        }
    }
}
