using Yakku.Application.Auth;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Models;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Services;
using Yakku.Application.System.Validators;
using Yakku.Domain.Enums;
using Xunit;

namespace Yakku.Application.Tests.System;

public class SystemServiceTests
{
    [Fact]
    public async Task DecryptOtp_FromStoredChallenge_ReturnsPlaintextOtp()
    {
        var fixture = SystemFixture.Create();
        var email = "user@example.com";
        await fixture.Store.SetAsync(email, NewChallenge(email, "483921"), OtpOptions.Ttl);

        var result = await fixture.Service.DecryptOtpAsync(new DecryptOtpRequest { Email = email });

        Assert.Equal("483921", result.Otp);
        Assert.Equal(email, result.Email);
        Assert.Equal("Registration", result.Purpose);
        Assert.Equal(0, result.AttemptCount);
        Assert.NotNull(result.ExpiresInSeconds);
        Assert.True(result.ExpiresInSeconds <= OtpOptions.ExpiresInSeconds);
    }

    [Fact]
    public async Task DecryptOtp_FromProvidedHash_ReturnsPlaintextOtp()
    {
        var fixture = SystemFixture.Create();
        var email = "user@example.com";
        var otpHash = OtpHasher.Hash(email, "000123");

        var result = await fixture.Service.DecryptOtpAsync(new DecryptOtpRequest
        {
            Email = email,
            OtpHash = otpHash
        });

        Assert.Equal("000123", result.Otp);
        Assert.Equal(otpHash, result.OtpHash);
        Assert.Null(result.Purpose);
    }

    [Fact]
    public async Task DecryptOtp_WhenChallengeMissing_ThrowsNotFound()
    {
        var fixture = SystemFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.DecryptOtpAsync(new DecryptOtpRequest { Email = "missing@example.com" }));

        Assert.Equal(404, exception.StatusCode);
        Assert.Equal("OTP_NOT_FOUND", exception.ErrorCode);
    }

    [Fact]
    public async Task DecryptOtp_WhenHashDoesNotMatch_ThrowsInvalid()
    {
        var fixture = SystemFixture.Create();

        var exception = await Assert.ThrowsAsync<AppException>(() =>
            fixture.Service.DecryptOtpAsync(new DecryptOtpRequest
            {
                Email = "user@example.com",
                OtpHash = "not-a-valid-otp-hash"
            }));

        Assert.Equal(400, exception.StatusCode);
        Assert.Equal("OTP_INVALID", exception.ErrorCode);
    }

    private static OtpChallenge NewChallenge(string email, string otp)
    {
        return new OtpChallenge
        {
            OtpHash = OtpHasher.Hash(email, otp),
            Purpose = OtpPurpose.Registration,
            DisplayName = "yakku@1233",
            AttemptCount = 0,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed class SystemFixture
    {
        public InMemoryOtpStore Store { get; }
        public SystemService Service { get; }

        private SystemFixture(InMemoryOtpStore store, SystemService service)
        {
            Store = store;
            Service = service;
        }

        public static SystemFixture Create()
        {
            var store = new InMemoryOtpStore();
            var service = new SystemService(store, new DecryptOtpValidator());
            return new SystemFixture(store, service);
        }
    }

    private sealed class InMemoryOtpStore : IOtpChallengeStore
    {
        public OtpChallenge? Challenge { get; set; }

        public Task<OtpChallenge?> GetAsync(string email, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Challenge);
        }

        public Task SetAsync(string email, OtpChallenge challenge, TimeSpan ttl, CancellationToken cancellationToken = default)
        {
            Challenge = challenge;
            return Task.CompletedTask;
        }

        public Task<bool> ReplaceKeepingTtlAsync(string email, OtpChallenge challenge, CancellationToken cancellationToken = default)
        {
            Challenge = challenge;
            return Task.FromResult(true);
        }

        public Task DeleteAsync(string email, CancellationToken cancellationToken = default)
        {
            Challenge = null;
            return Task.CompletedTask;
        }
    }
}
