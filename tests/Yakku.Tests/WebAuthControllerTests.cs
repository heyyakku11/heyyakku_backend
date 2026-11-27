using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Yakku.API.Controllers;
using Yakku.API.Guests;
using Yakku.API.Media;
using Yakku.Application.Auth;
using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Interfaces;
using Yakku.Application.Auth.Services;
using Yakku.Application.Common.Responses;
using Yakku.Application.Guests;
using Yakku.Application.Guests.DTOs;
using Yakku.Application.Guests.Interfaces;

namespace Yakku.Tests;

/// <summary>The website login route: one 15-minute token, no refresh token, and the guest claim around it.</summary>
public class WebAuthControllerTests
{
    [Fact]
    public async Task Verify_ReturnsOnlyAFifteenMinuteAccessToken_NoRefreshToken()
    {
        var h = new Harness(GuestClaimResult.Claimed);

        var result = await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var body = Assert.IsType<ApiResponse<WebVerifyOtpResponse>>(ok.Value);
        Assert.Equal(Harness.Token, body.Data!.AccessToken);
        Assert.Equal(900, body.Data.ExpiresInSeconds);
        var json = System.Text.Json.JsonSerializer.Serialize(
            ok.Value,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        Assert.DoesNotContain("refresh", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("userId", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Verify_UsesTheWebVerifyPath_NeverTheAppSessionPath()
    {
        var h = new Harness(GuestClaimResult.Claimed);

        await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        Assert.Equal(1, h.Auth.WebVerifyCalls);
        Assert.Equal(0, h.Auth.AppVerifyCalls);
        Assert.Equal(h.Auth.Response.UserId, h.Sessions.LastUserId);
    }

    [Theory]
    [InlineData(GuestClaimResult.Claimed, "claimed")]
    [InlineData(GuestClaimResult.NotFound, "none")]
    [InlineData(GuestClaimResult.Expired, "none")]
    [InlineData(GuestClaimResult.Failed, "failed")]
    public async Task Verify_ReportsWhatHappenedToTheGuest(GuestClaimResult outcome, string expected)
    {
        var h = new Harness(outcome);

        var ok = Assert.IsType<OkObjectResult>(await h.Controller.VerifyOtp(h.Request, CancellationToken.None));

        Assert.Equal(expected, Assert.IsType<ApiResponse<WebVerifyOtpResponse>>(ok.Value).Data!.GuestClaim);
    }

    [Theory]
    [InlineData(GuestClaimResult.Claimed)]
    [InlineData(GuestClaimResult.Expired)]
    [InlineData(GuestClaimResult.NotFound)]
    public async Task Verify_ClearsTheGuestCookie_OnceTheTokenIsUseless(GuestClaimResult outcome)
    {
        var h = new Harness(outcome);

        await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        Assert.Contains(h.Http.Response.Headers.SetCookie, value => value!.StartsWith(GuestCookieService.CookieName + "="));
    }

    [Fact]
    public async Task Verify_KeepsTheGuestCookie_WhenTheClaimFailed_AndStillSignsIn()
    {
        var h = new Harness(GuestClaimResult.Failed);

        var result = await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.DoesNotContain(h.Http.Response.Headers.SetCookie, value => value!.Contains(GuestCookieService.CookieName));
    }

    [Fact]
    public async Task Verify_WithoutAGuestToken_SkipsTheClaim_AndStillSignsIn()
    {
        var h = new Harness(GuestClaimResult.Claimed, sendToken: false);

        var ok = Assert.IsType<OkObjectResult>(await h.Controller.VerifyOtp(h.Request, CancellationToken.None));

        Assert.Equal(0, h.Claim.Calls);
        Assert.Equal("none", Assert.IsType<ApiResponse<WebVerifyOtpResponse>>(ok.Value).Data!.GuestClaim);
    }

    [Fact]
    public async Task Verify_ClaimsTheGuestBeforeTheTokenIsIssued()
    {
        var order = new List<string>();
        var h = new Harness(GuestClaimResult.Claimed, order: order);

        await h.Controller.VerifyOtp(h.Request, CancellationToken.None);

        Assert.Equal(["claim", "token"], order);
    }

    [Fact]
    public void WebSessionLifetime_IsFifteenMinutes_AndTheTokenCarriesIt()
    {
        var tokens = new TokenService(new string('k', 40), TimeSpan.FromDays(7));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(
            tokens.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid(), JwtOptions.WebSessionLifetime));

        Assert.Equal(TimeSpan.FromMinutes(15), JwtOptions.WebSessionLifetime);
        Assert.Equal(900, JwtOptions.WebSessionExpiresInSeconds);
        Assert.Equal(JwtOptions.WebSessionLifetime, jwt.ValidTo - jwt.ValidFrom);
    }

    [Fact]
    public void TheAppTokenKeepsItsConfiguredLifetime()
    {
        var tokens = new TokenService(new string('k', 40), TimeSpan.FromHours(2));

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(tokens.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(TimeSpan.FromHours(2), jwt.ValidTo - jwt.ValidFrom);
    }

    private sealed class Harness
    {
        public const string GuestToken = "guest-token-for-tests";
        public const string Token = "web-access-token";

        public Harness(GuestClaimResult outcome, bool sendToken = true, List<string>? order = null)
        {
            Auth = new StubAuth(new VerifyOtpResponse
            {
                UserId = Guid.NewGuid(),
                IsNewUser = false
            });
            Sessions = new StubSessions(order);
            Claim = new StubClaim(outcome, order);
            Http = new DefaultHttpContext();
            if (sendToken)
            {
                Http.Request.Headers[MediaCallerResolver.GuestTokenHeaderName] = GuestToken;
            }

            Controller = new WebAuthController(
                Auth,
                Sessions,
                Claim,
                new GuestCookieService(new StubIdentity()),
                NullLogger<WebAuthController>.Instance)
            {
                ControllerContext = new ControllerContext { HttpContext = Http }
            };
        }

        public StubAuth Auth { get; }
        public StubSessions Sessions { get; }
        public StubClaim Claim { get; }
        public DefaultHttpContext Http { get; }
        public WebAuthController Controller { get; }
        public WebVerifyOtpRequest Request { get; } = new() { PhoneNumber = "+15550000000", Otp = "123456" };
    }

    private sealed class StubAuth : IAuthService
    {
        public StubAuth(VerifyOtpResponse response) => Response = response;

        public VerifyOtpResponse Response { get; }
        public int WebVerifyCalls { get; private set; }
        public int AppVerifyCalls { get; private set; }

        public Task<RequestOtpResponse> RequestOtpAsync(RequestOtpRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<RequestOtpResponse> ResendOtpAsync(ResendOtpRequest request, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<VerifyOtpResponse> VerifyOtpAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
        {
            AppVerifyCalls++;
            return Task.FromResult(Response);
        }

        public Task<VerifyOtpResponse> VerifyOtpForWebAsync(VerifyOtpRequest request, CancellationToken cancellationToken = default)
        {
            WebVerifyCalls++;
            return Task.FromResult(Response);
        }
    }

    private sealed class StubSessions : IWebSessionService
    {
        private readonly List<string>? _order;

        public StubSessions(List<string>? order) => _order = order;

        public Guid LastUserId { get; private set; }

        public Task<string> StartAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            _order?.Add("token");
            LastUserId = userId;
            return Task.FromResult(Harness.Token);
        }
    }

    private sealed class StubClaim : IGuestMediaClaimService
    {
        private readonly GuestClaimResult _outcome;
        private readonly List<string>? _order;

        public StubClaim(GuestClaimResult outcome, List<string>? order)
        {
            _outcome = outcome;
            _order = order;
        }

        public int Calls { get; private set; }

        public Task<GuestClaimResult> ClaimAndDeleteGuestAsync(string? rawGuestToken, Guid userId, CancellationToken cancellationToken = default)
            => TryClaimAsync(rawGuestToken, userId, cancellationToken);

        public Task<GuestClaimResult> TryClaimAsync(string? rawGuestToken, Guid userId, CancellationToken cancellationToken = default)
        {
            Calls++;
            _order?.Add("claim");
            return Task.FromResult(_outcome);
        }
    }

    private sealed class StubIdentity : IGuestIdentityService
    {
        public Task<GuestEstablishResult> EstablishAsync(
            string? rawToken,
            string? idempotencyKey = null,
            GuestClientInfo? client = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
