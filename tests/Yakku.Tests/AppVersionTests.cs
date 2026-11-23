using FluentValidation;
using Microsoft.Extensions.Options;
using Yakku.Application.AppVersion;
using Yakku.Application.AppVersion.DTOs;
using Yakku.Application.AppVersion.Services;
using Yakku.Application.AppVersion.Validators;
using Yakku.Application.Configuration;

namespace Yakku.Tests;

public class AppVersionTests
{
    [Theory]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("2.0.0-beta+build", 2, 0, 0)]
    public void SemVersion_ParsesCoreParts_AndIgnoresMetadata(string input, int major, int minor, int patch)
    {
        Assert.True(SemVersion.TryParse(input, out var version));
        Assert.Equal(new SemVersion.Value(major, minor, patch), version);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1")]
    [InlineData("1.2.3.4")]
    [InlineData("-1.0.0")]
    public void SemVersion_RejectsInvalidValues(string input)
    {
        Assert.False(SemVersion.TryParse(input, out _));
    }

    [Fact]
    public void SemVersion_OrdersByMajorMinorPatch()
    {
        Assert.True(SemVersion.Compare("1.0.0", "1.0.1") < 0);
        Assert.True(SemVersion.Compare("1.2.0", "1.1.9") > 0);
        Assert.Equal(0, SemVersion.Compare("2.0.0-rc", "2.0.0"));
    }

    [Fact]
    public async Task BelowMinSupported_RequiresForceUpdate()
    {
        var response = await CreateService(
            androidMin: "1.3.0",
            androidLatest: "1.4.0",
            storeUrl: "https://play.google.com/store/apps/details?id=com.yakku")
            .CheckAsync(new AppVersionCheckRequest { Platform = "android", Version = "1.2.0" });

        Assert.Equal("android", response.Platform);
        Assert.Equal("1.2.0", response.CurrentVersion);
        Assert.Equal("1.3.0", response.MinSupportedVersion);
        Assert.Equal("1.4.0", response.LatestVersion);
        Assert.True(response.ForceUpdate);
        Assert.True(response.UpdateRequired);
        Assert.Equal("https://play.google.com/store/apps/details?id=com.yakku", response.StoreUrl);
        Assert.Equal("Please update to continue using Yakku.", response.Message);
    }

    [Fact]
    public async Task BetweenMinAndLatest_RequiresSoftUpdateOnly()
    {
        var response = await CreateService(androidMin: "1.2.0", androidLatest: "1.4.0")
            .CheckAsync(new AppVersionCheckRequest { Platform = "Android", Version = "1.3.0" });

        Assert.False(response.ForceUpdate);
        Assert.True(response.UpdateRequired);
        Assert.Equal("Please update to continue using Yakku.", response.Message);
    }

    [Fact]
    public async Task AtOrAboveLatest_IsOk()
    {
        var response = await CreateService(androidMin: "1.2.0", androidLatest: "1.4.0")
            .CheckAsync(new AppVersionCheckRequest { Platform = "android", Version = "1.4.0" });

        Assert.False(response.ForceUpdate);
        Assert.False(response.UpdateRequired);
        Assert.Equal(string.Empty, response.Message);
    }

    [Fact]
    public async Task Ios_UsesIosConfig()
    {
        var response = await CreateService(
                iosMin: "2.0.0",
                iosLatest: "2.1.0",
                iosStoreUrl: "https://apps.apple.com/app/id123")
            .CheckAsync(new AppVersionCheckRequest { Platform = "ios", Version = "1.9.9" });

        Assert.Equal("ios", response.Platform);
        Assert.True(response.ForceUpdate);
        Assert.Equal("https://apps.apple.com/app/id123", response.StoreUrl);
        Assert.Equal("2.0.0", response.MinSupportedVersion);
    }

    [Fact]
    public async Task LatestBelowMin_IsClampedToMin()
    {
        var response = await CreateService(androidMin: "2.0.0", androidLatest: "1.0.0")
            .CheckAsync(new AppVersionCheckRequest { Platform = "android", Version = "1.5.0" });

        Assert.True(response.ForceUpdate);
        Assert.Equal("2.0.0", response.LatestVersion);
    }

    [Theory]
    [InlineData(null, "1.0.0")]
    [InlineData("", "1.0.0")]
    [InlineData("windows", "1.0.0")]
    [InlineData("android", null)]
    [InlineData("android", "")]
    [InlineData("android", "not-a-version")]
    public async Task InvalidPlatformOrVersion_FailsValidation(string? platform, string? version)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<ValidationException>(() =>
            service.CheckAsync(new AppVersionCheckRequest { Platform = platform, Version = version }));
    }

    [Fact]
    public void Validator_AcceptsValidQuery()
    {
        var result = new AppVersionCheckValidator().Validate(new AppVersionCheckRequest
        {
            Platform = "ios",
            Version = "1.2.3"
        });

        Assert.True(result.IsValid);
    }

    private static AppVersionService CreateService(
        string androidMin = "1.0.0",
        string androidLatest = "1.0.0",
        string storeUrl = "",
        string iosMin = "1.0.0",
        string iosLatest = "1.0.0",
        string iosStoreUrl = "")
    {
        var options = Options.Create(new AppVersionOptions
        {
            Message = "Please update to continue using Yakku.",
            Android = new AppVersionPlatformOptions
            {
                MinSupported = androidMin,
                Latest = androidLatest,
                StoreUrl = storeUrl
            },
            Ios = new AppVersionPlatformOptions
            {
                MinSupported = iosMin,
                Latest = iosLatest,
                StoreUrl = iosStoreUrl
            }
        });

        return new AppVersionService(options, new AppVersionCheckValidator());
    }
}
