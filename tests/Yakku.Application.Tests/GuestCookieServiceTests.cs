using Microsoft.AspNetCore.Http;
using Yakku.API.Guests;
using Yakku.Application.Guests.Interfaces;
using Yakku.Application.Guests.Services;
using Yakku.Domain.Entities;
using Xunit;

namespace Yakku.Application.Tests.Guests;

public class GuestCookieServiceTests
{
    [Fact]
    public async Task Ensure_NoCookie_SetsHttpOnlyLaxCookie()
    {
        var fixture = CookieFixture.Create();
        var context = HttpsContext();

        var guestId = await fixture.Cookies.EnsureAsync(context);

        var header = context.Response.Headers.SetCookie.ToString();
        Assert.Equal(fixture.Guests.Items[0].Id, guestId);
        Assert.Contains($"{GuestCookieService.CookieName}={fixture.Generator.Tokens[0]}", header);
        Assert.Contains("httponly", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", header, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", header, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, context.Response.Body.Length);
        Assert.DoesNotContain(fixture.Generator.Tokens[0], fixture.Guests.Items[0].TokenHash);
    }

    [Fact]
    public async Task Ensure_ValidCookie_DoesNotReplaceCookie()
    {
        var fixture = CookieFixture.Create();
        var first = HttpsContext();
        var guestId = await fixture.Cookies.EnsureAsync(first);
        var token = fixture.Generator.Tokens[0];

        var second = HttpsContext();
        second.Request.Headers.Cookie = $"{GuestCookieService.CookieName}={token}";
        var again = await fixture.Cookies.EnsureAsync(second);

        Assert.Equal(guestId, again);
        Assert.Single(fixture.Guests.Items);
        Assert.Equal(0, second.Response.Headers.SetCookie.Count);
    }

    [Fact]
    public async Task Ensure_UnknownCookie_ReplacesCookie()
    {
        var fixture = CookieFixture.Create();
        var first = HttpsContext();
        var originalId = await fixture.Cookies.EnsureAsync(first);

        var second = HttpsContext();
        second.Request.Headers.Cookie = $"{GuestCookieService.CookieName}=unknown-token";
        var replacementId = await fixture.Cookies.EnsureAsync(second);

        var header = second.Response.Headers.SetCookie.ToString();
        Assert.NotEqual(originalId, replacementId);
        Assert.Equal(2, fixture.Guests.Items.Count);
        Assert.Contains($"{GuestCookieService.CookieName}={fixture.Generator.Tokens[1]}", header);
        Assert.DoesNotContain("unknown-token", header);
    }

    [Fact]
    public void CreateOptions_HttpRequest_IsNotSecure()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "http";

        var options = GuestCookieService.CreateOptions(context.Request);

        Assert.True(options.HttpOnly);
        Assert.False(options.Secure);
        Assert.Equal(SameSiteMode.Lax, options.SameSite);
        Assert.Equal(TimeSpan.FromDays(365), options.MaxAge);
        Assert.Equal("/", options.Path);
    }

    private static DefaultHttpContext HttpsContext()
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = "https";
        context.Response.Body = new MemoryStream();
        return context;
    }

    private sealed class CookieFixture
    {
        public required GuestCookieService Cookies { get; init; }
        public required FakeGuestRepository Guests { get; init; }
        public required SequentialTokenGenerator Generator { get; init; }

        public static CookieFixture Create()
        {
            var guests = new FakeGuestRepository();
            var generator = new SequentialTokenGenerator();
            var identity = new GuestIdentityService(guests, generator, new GuestTokenHasher());

            return new CookieFixture
            {
                Cookies = new GuestCookieService(identity),
                Guests = guests,
                Generator = generator
            };
        }
    }

    private sealed class SequentialTokenGenerator : IGuestTokenGenerator
    {
        public List<string> Tokens { get; } = [];

        public string Generate()
        {
            var token = $"guest-token-{Tokens.Count + 1}";
            Tokens.Add(token);
            return token;
        }
    }

    private sealed class FakeGuestRepository : IGuestRepository
    {
        public List<Guest> Items { get; } = [];

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
            return Task.CompletedTask;
        }
    }
}
