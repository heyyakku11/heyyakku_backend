using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.API.Controllers;
using Yakku.API.Guests;
using Yakku.API.Media;
using Yakku.Application.Audit;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Guests;
using Yakku.Application.Guests.DTOs;
using Yakku.Application.Guests.Interfaces;
using Yakku.Domain.Enums;
using Yakku.Infrastructure.Jobs;

namespace Yakku.Tests;

public class AuthReliabilityTests
{
    [Fact]
    public async Task Verify_Succeeds_WhenTheGuestClaimFails_AndKeepsTheGuestCredential()
    {
        var h = new ControllerHarness(_ => Task.FromResult(GuestClaimResult.Failed));

        var result = await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(result).StatusCode);
        Assert.DoesNotContain(h.Http.Response.Headers.SetCookie, value => value!.Contains(GuestCookieService.CookieName));
    }

    [Fact]
    public async Task Verify_Succeeds_EvenIfTheClaimServiceThrowsUnexpectedly()
    {
        var h = new ControllerHarness(_ => throw new InvalidOperationException("boom"));

        var result = await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        Assert.Equal(StatusCodes.Status200OK, Assert.IsType<OkObjectResult>(result).StatusCode);
        Assert.DoesNotContain(h.Http.Response.Headers.SetCookie, value => value!.Contains(GuestCookieService.CookieName));
    }

    [Theory]
    [InlineData(GuestClaimResult.Claimed)]
    [InlineData(GuestClaimResult.Expired)]
    [InlineData(GuestClaimResult.NotFound)]
    public async Task Verify_ClearsTheGuestCookie_OnceTheTokenIsUseless(GuestClaimResult outcome)
    {
        var h = new ControllerHarness(_ => Task.FromResult(outcome));

        var result = await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Contains(h.Http.Response.Headers.SetCookie, value => value!.StartsWith(GuestCookieService.CookieName + "="));
    }

    [Fact]
    public async Task Verify_WithoutAGuestToken_DoesNotTouchTheClaimService()
    {
        var h = new ControllerHarness(_ => Task.FromResult(GuestClaimResult.Claimed), sendToken: false);

        var result = await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal(0, h.Claim.Calls);
    }

    [Fact]
    public async Task Verify_PassesTheTokenToTheClaim_WithoutEchoingItInTheResponseHeaders()
    {
        var h = new ControllerHarness(_ => Task.FromResult(GuestClaimResult.Claimed));

        await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        Assert.Equal(1, h.Claim.Calls);
        Assert.Equal(h.Response.UserId, h.Claim.LastUserId);
        Assert.Equal(ControllerHarness.GuestToken, h.Claim.LastToken);
    }

    [Fact]
    public async Task BackgroundJob_Failure_IsRecordedAndNeverEscapes()
    {
        var audit = new RecordingAuditLogWriter();
        var services = new ServiceCollection();
        services.AddSingleton<IAuditLogWriter>(audit);
        await using var provider = services.BuildServiceProvider();
        var worker = new ThrowingWorker(provider.GetRequiredService<IServiceScopeFactory>());

        await worker.RunOnceAsync(CancellationToken.None);

        var log = Assert.Single(audit.System);
        Assert.Equal(SystemLogCategory.BackgroundJob, log.Category);
        Assert.Equal(SystemLogSeverity.Error, log.Severity);
        Assert.Equal(nameof(InvalidOperationException), log.ExceptionType);
        Assert.Equal("test_job", log.Source);
    }

    private sealed class ThrowingWorker : PeriodicJobWorker
    {
        public ThrowingWorker(IServiceScopeFactory scopeFactory)
            : base(scopeFactory, NullLogger.Instance, "test_job", TimeSpan.Zero, TimeSpan.FromHours(1))
        {
        }

        protected override Task RunAsync(IServiceProvider services, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("secret detail that must not be persisted");
        }
    }

    private sealed class ControllerHarness
    {
        public const string GuestToken = "guest-token-for-tests";

        public ControllerHarness(Func<string?, Task<GuestClaimResult>> claim, bool sendToken = true)
        {
            Response = new VerifyOtpResponse { UserId = Guid.NewGuid(), Purpose = "Registration" };
            Claim = new StubClaimService(claim);
            Http = new DefaultHttpContext();
            if (sendToken)
            {
                Http.Request.Headers[MediaCallerResolver.GuestTokenHeaderName] = GuestToken;
            }

            Controller = new AuthController(
                new StubAuthService(Response),
                null!,
                Claim,
                new GuestCookieService(new StubIdentityService()),
                NullLogger<AuthController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = Http }
            };
        }

        public VerifyOtpResponse Response { get; }

        public StubClaimService Claim { get; }

        public DefaultHttpContext Http { get; }

        public AuthController Controller { get; }

        public VerifyOtpRequest Request { get; } = new() { PhoneNumber = "+15550000000", Otp = "123456" };
    }

    private sealed class StubClaimService : IGuestMediaClaimService
    {
        private readonly Func<string?, Task<GuestClaimResult>> _claim;

        public StubClaimService(Func<string?, Task<GuestClaimResult>> claim)
        {
            _claim = claim;
        }

        public int Calls { get; private set; }

        public string? LastToken { get; private set; }

        public Guid LastUserId { get; private set; }

        public Task<GuestClaimResult> ClaimAndDeleteGuestAsync(string? rawGuestToken, Guid userId, CancellationToken cancellationToken = default)
            => TryClaimAsync(rawGuestToken, userId, cancellationToken);

        public Task<GuestClaimResult> TryClaimAsync(string? rawGuestToken, Guid userId, CancellationToken cancellationToken = default)
        {
            Calls++;
            LastToken = rawGuestToken;
            LastUserId = userId;
            return _claim(rawGuestToken);
        }
    }

    private sealed class StubAuthService : IAuthService
    {
        private readonly VerifyOtpResponse _response;

        public StubAuthService(VerifyOtpResponse response)
        {
            _response = response;
        }

        public Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RequestOtpResponse> ResendOtpAsync(ResendOtpRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
            => Task.FromResult(_response);
    }

    private sealed class StubIdentityService : IGuestIdentityService
    {
        public Task<GuestEstablishResult> EstablishAsync(string? rawToken, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
