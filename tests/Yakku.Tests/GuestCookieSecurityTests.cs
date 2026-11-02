using Microsoft.AspNetCore.Http;
using Yakku.API.Guests;
using Yakku.API.Media;
using Yakku.Application.Guests.Services;
using Yakku.Domain.Entities;

namespace Yakku.Tests;

/// <summary>Cookie audit: what the guest cookie looks like on the wire, and how tampered or stale values behave.</summary>
public class GuestCookieSecurityTests
{
    private static readonly GuestTokenHasher Hasher = new();

    private static (GuestCookieService Cookies, InMemoryGuestRepository Guests) NewService()
    {
        var guests = new InMemoryGuestRepository();
        var identity = new GuestIdentityService(
            guests, new GuestTokenGenerator(), Hasher, new RecordingAuditLogWriter());
        return (new GuestCookieService(identity), guests);
    }

    private static DefaultHttpContext NewContext(string scheme = "https")
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = scheme;
        http.Request.Host = new HostString("api.example.com");
        return http;
    }

    private static string SetCookieHeader(HttpContext http)
    {
        return Assert.Single(http.Response.Headers.SetCookie, value => value!.StartsWith(GuestCookieService.CookieName + "="))!;
    }

    [Fact]
    public async Task FirstVisit_IssuesAnOpaqueHighEntropyToken_InAnHttpOnlySecureLaxCookie()
    {
        var (cookies, guests) = NewService();
        var http = NewContext();

        var result = await cookies.EnsureAsync(http);
        var header = SetCookieHeader(http);

        Assert.Single(guests.All);
        Assert.NotNull(result.RawTokenToSet);
        Assert.Equal(64, result.RawTokenToSet!.Length); // 256 bits, hex
        Assert.Matches("^[0-9A-F]{64}$", result.RawTokenToSet);
        // Only the hash is stored.
        Assert.Equal(Hasher.Hash(result.RawTokenToSet), guests.All[0].GuestTokenHash);
        Assert.DoesNotContain(result.RawTokenToSet, guests.All[0].GuestTokenHash);

        var lower = header.ToLowerInvariant();
        Assert.Contains("httponly", lower);
        Assert.Contains("secure", lower);
        Assert.Contains("samesite=lax", lower);
        Assert.Contains("path=/", lower);
        Assert.Contains("max-age=2592000", lower); // 30 days, a persistent cookie
        Assert.DoesNotContain("domain=", lower);   // host-only
    }

    [Fact]
    public async Task OverPlainHttp_TheCookieIsIssuedWithoutTheSecureFlag()
    {
        // Documents the behaviour behind a TLS-terminating proxy: Secure follows Request.IsHttps, and the API
        // listens on plain HTTP, so unless forwarded headers are applied the flag is missing.
        var (cookies, _) = NewService();
        var http = NewContext(scheme: "http");
        http.Request.Headers["X-Forwarded-Proto"] = "https";

        await cookies.EnsureAsync(http);

        Assert.DoesNotContain("secure", SetCookieHeader(http).ToLowerInvariant().Replace("samesite", string.Empty));
    }

    [Fact]
    public async Task ValidCookie_IsReused_WithoutSettingAnotherCookie()
    {
        var (cookies, guests) = NewService();
        var first = NewContext();
        var created = await cookies.EnsureAsync(first);

        var again = NewContext();
        again.Request.Headers.Cookie = $"{GuestCookieService.CookieName}={created.RawTokenToSet}";
        var reused = await cookies.EnsureAsync(again);

        Assert.Equal(created.GuestId, reused.GuestId);
        Assert.Single(guests.All);
        Assert.Equal(0, again.Response.Headers.SetCookie.Count); // the cookie is not rotated or extended
    }

    [Theory]
    [InlineData("tampered")]
    [InlineData("0000000000000000000000000000000000000000000000000000000000000000")]
    [InlineData("%00%00")]
    public async Task TamperedCookie_DoesNotAuthenticateAsTheOriginalGuest(string tampered)
    {
        var (cookies, guests) = NewService();
        var first = NewContext();
        var original = await cookies.EnsureAsync(first);

        var http = NewContext();
        http.Request.Headers.Cookie = $"{GuestCookieService.CookieName}={tampered}";
        var result = await cookies.EnsureAsync(http);

        // The bad value matches nobody: it is never treated as the original guest. It is replaced by a brand new
        // anonymous identity (rate limited by the guest-create policy) with a fresh cookie.
        Assert.NotEqual(original.GuestId, result.GuestId);
        Assert.NotNull(result.RawTokenToSet);
        Assert.NotEqual(tampered, result.RawTokenToSet);
        Assert.Equal(2, guests.All.Count);
    }

    [Fact]
    public async Task CookieOfADeletedGuest_AsAfterLoginClaim_IsRejected_AndReplaced()
    {
        var (cookies, guests) = NewService();
        var created = await cookies.EnsureAsync(NewContext());
        guests.Delete(guests.All[0]); // what claiming the guest at sign-in does

        var http = NewContext();
        http.Request.Headers.Cookie = $"{GuestCookieService.CookieName}={created.RawTokenToSet}";
        var result = await cookies.EnsureAsync(http);

        Assert.NotEqual(created.GuestId, result.GuestId);
        Assert.Single(guests.All);
        Assert.NotEqual(created.RawTokenToSet, result.RawTokenToSet);
    }

    [Fact]
    public async Task ExpiredCookie_IsNeverRevived_AndIsReplaced()
    {
        var (cookies, guests) = NewService();
        var raw = "EXPIRED-TOKEN-FOR-TEST";
        guests.Seed(new Guest(Hasher.Hash(raw), DateTime.UtcNow.AddDays(-40)));

        var http = NewContext();
        http.Request.Headers.Cookie = $"{GuestCookieService.CookieName}={raw}";
        var result = await cookies.EnsureAsync(http);

        Assert.NotNull(result.RawTokenToSet);
        Assert.NotEqual(raw, result.RawTokenToSet);
        Assert.Equal(2, guests.All.Count);
    }

    [Fact]
    public void Clear_ExpiresTheCookie_WithTheSameScopeAttributes()
    {
        var (cookies, _) = NewService();
        var http = NewContext();

        cookies.Clear(http);
        var lower = SetCookieHeader(http).ToLowerInvariant();

        Assert.Contains("expires=thu, 01 jan 1970", lower);
        Assert.Contains("path=/", lower);
        Assert.Contains("httponly", lower);
        Assert.Contains("secure", lower);
        Assert.Contains("samesite=lax", lower);
    }

    [Fact]
    public void TokenSource_HeaderWinsOverCookie_AndMissingMeansNoIdentity()
    {
        var http = NewContext();
        Assert.Null(MediaCallerResolver.ReadGuestToken(http));

        http.Request.Headers.Cookie = $"{GuestCookieService.CookieName}=from-cookie";
        Assert.Equal("from-cookie", MediaCallerResolver.ReadGuestToken(http));

        http.Request.Headers[MediaCallerResolver.GuestTokenHeaderName] = "from-header";
        Assert.Equal("from-header", MediaCallerResolver.ReadGuestToken(http));

        http.Request.Headers[MediaCallerResolver.GuestTokenHeaderName] = "   ";
        Assert.Equal("from-cookie", MediaCallerResolver.ReadGuestToken(http));
    }
}
