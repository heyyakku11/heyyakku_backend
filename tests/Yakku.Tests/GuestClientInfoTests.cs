using System.Net;
using Microsoft.AspNetCore.Http;
using Yakku.API.Guests;
using Yakku.Application.Audit;
using Yakku.Application.Guests.DTOs;
using Yakku.Application.Guests.Services;
using Yakku.Domain.Enums;

namespace Yakku.Tests;

public class GuestClientInfoTests
{
    private const string ChromeWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.6367.60 Safari/537.36";
    private const string EdgeWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36 Edg/124.0.2478.51";
    private const string SafariIPhone =
        "Mozilla/5.0 (iPhone; CPU iPhone OS 17_4 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Mobile/15E148 Safari/604.1";
    private const string ChromeAndroidPhone =
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.6367.82 Mobile Safari/537.36";
    private const string ChromeAndroidTablet =
        "Mozilla/5.0 (Linux; Android 13; SM-X710) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.6367.82 Safari/537.36";
    private const string FirefoxLinux =
        "Mozilla/5.0 (X11; Linux x86_64; rv:125.0) Gecko/20100101 Firefox/125.0";
    private const string SafariMac =
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.4 Safari/605.1.15";
    private const string Googlebot =
        "Mozilla/5.0 (compatible; Googlebot/2.1; +http://www.google.com/bot.html)";

    [Theory]
    [InlineData(ChromeWindows, "Chrome", "124.0.6367.60", "Windows", GuestDeviceType.Desktop)]
    [InlineData(EdgeWindows, "Edge", "124.0.2478.51", "Windows", GuestDeviceType.Desktop)]
    [InlineData(SafariIPhone, "Safari", "17.4", "iOS", GuestDeviceType.Mobile)]
    [InlineData(ChromeAndroidPhone, "Chrome", "124.0.6367.82", "Android", GuestDeviceType.Mobile)]
    [InlineData(ChromeAndroidTablet, "Chrome", "124.0.6367.82", "Android", GuestDeviceType.Tablet)]
    [InlineData(FirefoxLinux, "Firefox", "125.0", "Linux", GuestDeviceType.Desktop)]
    [InlineData(SafariMac, "Safari", "17.4", "macOS", GuestDeviceType.Desktop)]
    [InlineData("Dart/3.5 (dart:io)", "Dart", "3.5", null, GuestDeviceType.Unknown)]
    public void Parser_RecognisesCommonClients(
        string userAgent, string browser, string version, string? os, GuestDeviceType device)
    {
        var parsed = UserAgentParser.Parse(userAgent);

        Assert.Equal(browser, parsed.BrowserName);
        Assert.Equal(version, parsed.BrowserVersion);
        Assert.Equal(os, parsed.OsName);
        Assert.Equal(device, parsed.DeviceType);
    }

    [Fact]
    public void Parser_FlagsCrawlersAsBots()
    {
        Assert.Equal(GuestDeviceType.Bot, UserAgentParser.Parse(Googlebot).DeviceType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parser_ReturnsUnknown_WhenThereIsNoUserAgent(string? userAgent)
    {
        var parsed = UserAgentParser.Parse(userAgent);

        Assert.Null(parsed.BrowserName);
        Assert.Null(parsed.OsName);
        Assert.Equal(GuestDeviceType.Unknown, parsed.DeviceType);
    }

    [Fact]
    public void Reader_TakesEverythingFromHeaders()
    {
        var http = new DefaultHttpContext();
        http.Connection.RemoteIpAddress = IPAddress.Parse("::ffff:203.0.113.9");
        http.Request.Headers.UserAgent = ChromeAndroidPhone;
        http.Request.Headers.AcceptLanguage = "en-IN,en;q=0.9,hi;q=0.8";
        http.Request.Headers["X-Cookies-Enabled"] = "true";
        http.Request.Headers["X-Vercel-IP-Country"] = "in";

        var info = GuestClientInfoReader.From(http);

        Assert.Equal("203.0.113.9", info.IpAddress);
        Assert.Equal(ChromeAndroidPhone, info.UserAgent);
        Assert.Equal("Chrome", info.BrowserName);
        Assert.Equal("Android", info.OsName);
        Assert.Equal(GuestDeviceType.Mobile, info.DeviceType);
        Assert.Equal("en-IN", info.Language);
        Assert.Equal("IN", info.CountryCode);
        Assert.True(info.CookiesEnabled);
    }

    [Fact]
    public void Reader_LeavesMissingOrInvalidHeadersNull()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers.AcceptLanguage = "*";
        http.Request.Headers["X-Cookies-Enabled"] = "maybe";
        http.Request.Headers["CF-IPCountry"] = "XX";

        var info = GuestClientInfoReader.From(http);

        Assert.Null(info.IpAddress);
        Assert.Null(info.UserAgent);
        Assert.Null(info.Language);
        Assert.Null(info.CountryCode);
        Assert.Null(info.CookiesEnabled);
        Assert.Equal(GuestDeviceType.Unknown, info.DeviceType);
    }

    [Fact]
    public void Reader_ReadsFalseCookiesFlag()
    {
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Cookies-Enabled"] = "false";

        Assert.False(GuestClientInfoReader.From(http).CookiesEnabled);
    }

    [Fact]
    public async Task Establish_StoresClientInfoOnCreate_AndLeavesItAloneOnReuse()
    {
        var guests = new InMemoryGuestRepository();
        var hasher = new GuestTokenHasher();
        var identity = new GuestIdentityService(guests, new GuestTokenGenerator(), hasher, new RecordingAuditLogWriter());

        var created = await identity.EstablishAsync(
            null,
            new GuestClientInfo(
                IpAddress: "203.0.113.9",
                UserAgent: ChromeWindows,
                BrowserName: "Chrome",
                BrowserVersion: "124.0.6367.60",
                OsName: "Windows",
                DeviceType: GuestDeviceType.Desktop,
                Language: "en-IN",
                CountryCode: "in",
                CookiesEnabled: true));

        var stored = Assert.Single(guests.All);
        Assert.Equal(IPAddress.Parse("203.0.113.9"), stored.IpAddress);
        Assert.Equal(ChromeWindows, stored.UserAgent);
        Assert.Equal("Chrome", stored.BrowserName);
        Assert.Equal("124.0.6367.60", stored.BrowserVersion);
        Assert.Equal("Windows", stored.OsName);
        Assert.Equal(GuestDeviceType.Desktop, stored.DeviceType);
        Assert.Equal("en-IN", stored.Language);
        Assert.Equal("IN", stored.CountryCode);
        Assert.True(stored.CookiesEnabled);

        // Reusing the guest from another client must not overwrite what was recorded at creation.
        await identity.EstablishAsync(created.RawTokenToSet, new GuestClientInfo(IpAddress: "198.51.100.1", BrowserName: "Firefox"));

        Assert.Equal(IPAddress.Parse("203.0.113.9"), stored.IpAddress);
        Assert.Equal("Chrome", stored.BrowserName);
    }

    [Fact]
    public async Task Establish_WithoutClientInfo_StillCreatesTheGuest()
    {
        var guests = new InMemoryGuestRepository();
        var identity = new GuestIdentityService(
            guests, new GuestTokenGenerator(), new GuestTokenHasher(), new RecordingAuditLogWriter());

        var result = await identity.EstablishAsync(null);

        Assert.NotNull(result.RawTokenToSet);
        var stored = Assert.Single(guests.All);
        Assert.Null(stored.IpAddress);
        Assert.Null(stored.UserAgent);
        Assert.Null(stored.DeviceType);
    }
}
