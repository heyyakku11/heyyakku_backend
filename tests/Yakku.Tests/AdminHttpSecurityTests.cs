using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Yakku.API.AdminSecurity;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.AdminAuth.Security;
using Yakku.Application.AdminAuth.Services;
using Yakku.Application.Audit;
using Yakku.Application.Auth;
using Yakku.Domain.Entities;

namespace Yakku.Tests;

/// <summary>
/// Real HTTP against a real Kestrel host running the production authentication handler, cookie service, CSRF/Origin
/// middleware and policy (wired by the same AddAdminSecurity the app uses). Only the endpoints are stand-ins, so
/// what is under test is the middleware pipeline and cookies, not controller plumbing.
/// </summary>
public class AdminHttpSecurityTests
{
    private const string Trusted = "https://admin.example.test";
    private const string JwtSecret = "unit-test-jwt-secret-that-is-long-enough-1234567890";

    [Fact]
    public async Task NoCookie_OnAProtectedRoute_Is401()
    {
        await using var host = await AdminHttpHost.StartAsync();

        var response = await host.Get("/api/v1/admin/probe");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task MalformedAndUnknownCookies_Are401_AndTheDeadCookieIsCleared()
    {
        await using var host = await AdminHttpHost.StartAsync();

        foreach (var junk in new[] { "garbage", AdminSessionToken.Generate(), new string('A', 5000) })
        {
            var response = await host.Get("/api/v1/admin/probe", cookie: junk);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            var cleared = host.SetCookieOf(response);
            Assert.NotNull(cleared);
            Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ValidSession_CanReadWithGet_WithoutACsrfToken()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();

        var response = await host.Get("/api/v1/admin/probe", login.Cookie);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task StateChange_WithoutCsrfToken_IsRejected()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();

        var response = await host.Post("/api/v1/admin/probe", login.Cookie, csrf: null, origin: Trusted);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(host.Audit.Authentication, e => e.EventType == AuditEvents.Auth.CsrfRejected && e.FailureReason == "csrf_missing");
    }

    [Fact]
    public async Task StateChange_WithWrongCsrfToken_IsRejected()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();

        var response = await host.Post("/api/v1/admin/probe", login.Cookie, csrf: login.Csrf + "tampered", origin: Trusted);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(host.Audit.Authentication, e => e.FailureReason == "csrf_invalid");
    }

    [Fact]
    public async Task StateChange_WithAnotherSessionsCsrfToken_IsRejected()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var mine = await host.LoginAsync();
        var theirs = await host.LoginAsync();

        var response = await host.Post("/api/v1/admin/probe", mine.Cookie, csrf: theirs.Csrf, origin: Trusted);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StateChange_WithValidCsrfAndTrustedOrigin_Succeeds()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();

        var response = await host.Post("/api/v1/admin/probe", login.Cookie, login.Csrf, Trusted);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("https://admin.example.test.evil.example")]
    [InlineData("http://admin.example.test")]
    [InlineData("null")]
    public async Task CrossOriginStateChange_IsRejected_EvenWithAValidCookieAndCsrfToken(string origin)
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();

        var response = await host.Post("/api/v1/admin/probe", login.Cookie, login.Csrf, origin);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(host.Audit.Authentication, e => e.EventType == AuditEvents.Auth.OriginRejected);
    }

    [Fact]
    public async Task StateChange_WithNeitherOriginNorReferer_IsRejected()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();

        var response = await host.Post("/api/v1/admin/probe", login.Cookie, login.Csrf, origin: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task StateChange_RefererIsAFallbackForOrigin_ButOnlyATrustedOne()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();

        var good = await host.Post("/api/v1/admin/probe", login.Cookie, login.Csrf, origin: null, referer: Trusted + "/polls");
        var bad = await host.Post("/api/v1/admin/probe", login.Cookie, login.Csrf, origin: null, referer: "https://evil.example/x");

        Assert.Equal(HttpStatusCode.OK, good.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, bad.StatusCode);
    }

    [Fact]
    public async Task Login_FromAnUntrustedOrigin_IsRejected_SoLoginCsrfFails()
    {
        await using var host = await AdminHttpHost.StartAsync();

        var response = await host.PostLogin(origin: "https://evil.example");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Null(host.SetCookieOf(response));
        Assert.Empty(host.Sessions.All);
    }

    [Fact]
    public async Task SafeRequests_AreNeverCsrfOrOriginChecked()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();

        var response = await host.Get("/api/v1/admin/probe", login.Cookie, origin: "https://evil.example");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task LoginCookie_IsHttpOnly_Secure_Strict_HostOnly_WithAnExplicitLifetime()
    {
        await using var host = await AdminHttpHost.StartAsync(production: true);

        var response = await host.PostLogin(Trusted);
        var cookie = host.SetCookieOf(response)!;

        Assert.StartsWith("__Host-yakku_admin=", cookie);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("max-age=43200", cookie, StringComparison.OrdinalIgnoreCase); // 12 hour absolute limit
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheSessionTokenAndHash_NeverAppearInAResponseBody()
    {
        await using var host = await AdminHttpHost.StartAsync();

        var response = await host.PostLogin(Trusted);
        var body = await response.Content.ReadAsStringAsync();
        var token = AdminHttpHost.CookieValue(host.SetCookieOf(response)!);

        Assert.DoesNotContain(token, body);
        Assert.DoesNotContain(AdminSessionToken.Hash(token), body);
        Assert.DoesNotContain("refresh", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accessToken", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Logout_RevokesOnTheServer_ClearsTheCookieWithMatchingAttributes_AndTheOldCookieIsDead()
    {
        await using var host = await AdminHttpHost.StartAsync(production: true);
        var login = await host.LoginAsync(production: true);

        var logout = await host.Post("/api/v1/admin/auth/logout", login.Cookie, login.Csrf, Trusted);

        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        var cleared = host.SetCookieOf(logout)!;
        Assert.Contains("expires=Thu, 01 Jan 1970", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cleared, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", cleared, StringComparison.OrdinalIgnoreCase);

        // Deleting the browser cookie is not what ends the session: replaying the copied cookie still fails.
        var replay = await host.Get("/api/v1/admin/probe", login.Cookie);
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.NotNull(Assert.Single(host.Sessions.All).RevokedAt);
    }

    [Fact]
    public async Task ARevokedSession_CannotAuthenticateAnyRequest()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();
        await host.SessionService.RevokeAllAsync(host.Admin.Id, AdminSessionRevocationReasons.PasswordChanged);

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Get("/api/v1/admin/probe", login.Cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Post("/api/v1/admin/probe", login.Cookie, login.Csrf, Trusted)).StatusCode);
    }

    [Fact]
    public async Task ADisabledAdmin_LosesAccessOnTheVeryNextRequest()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();
        AdminTestAdmins.Disable(host.Admin);

        var response = await host.Get("/api/v1/admin/probe", login.Cookie);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task IdleAndAbsoluteExpiry_AreEnforcedOverHttp()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var idle = await host.LoginAsync();
        host.Time.Advance(host.Settings.Session.IdleTimeout + TimeSpan.FromMinutes(1));

        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Get("/api/v1/admin/probe", idle.Cookie)).StatusCode);
    }

    [Fact]
    public async Task Rotation_HandsTheBrowserANewCookie_AndTheOldOneStopsAfterTheGraceWindow()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();
        host.Time.Advance(host.Settings.Session.RotationInterval + TimeSpan.FromSeconds(1));

        var rotated = await host.Get("/api/v1/admin/probe", login.Cookie);

        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var newCookie = AdminHttpHost.CookieValue(host.SetCookieOf(rotated)!);
        Assert.NotEqual(login.Cookie, newCookie);
        Assert.Equal(HttpStatusCode.OK, (await host.Get("/api/v1/admin/probe", newCookie)).StatusCode);

        host.Time.Advance(host.Settings.Session.RotationGrace + TimeSpan.FromSeconds(1));
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Get("/api/v1/admin/probe", login.Cookie)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Get("/api/v1/admin/probe", newCookie)).StatusCode);
    }

    [Fact]
    public async Task ConcurrentRequestsDuringRotation_NeverLogTheAdminOut()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();
        host.Time.Advance(host.Settings.Session.RotationInterval + TimeSpan.FromSeconds(1));

        var responses = await Task.WhenAll(Enumerable.Range(0, 15).Select(_ => host.Get("/api/v1/admin/probe", login.Cookie)));

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single(responses, r => host.SetCookieOf(r) is not null);
    }

    [Fact]
    public async Task AnOldAdminBearerJwt_IsNotAcceptedAnywhere()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var legacy = host.MakeJwt(host.Admin.Id, admin: true);

        var adminRoute = await host.Get("/api/v1/admin/probe", bearer: legacy);
        var userRoute = await host.Get("/api/v1/user/probe", bearer: legacy);

        Assert.Equal(HttpStatusCode.Unauthorized, adminRoute.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, userRoute.StatusCode);
    }

    [Fact]
    public async Task AnOrdinaryUsersToken_CannotReachAnAdminRoute()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var userJwt = host.MakeJwt(Guid.NewGuid(), admin: false);

        Assert.Equal(HttpStatusCode.OK, (await host.Get("/api/v1/user/probe", bearer: userJwt)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Get("/api/v1/admin/probe", bearer: userJwt)).StatusCode);
    }

    [Fact]
    public async Task AdminRoutes_AreNeverCached()
    {
        await using var host = await AdminHttpHost.StartAsync();
        var login = await host.LoginAsync();

        var response = await host.Get("/api/v1/admin/probe", login.Cookie);

        Assert.Contains("no-store", string.Join(",", response.Headers.GetValues("Cache-Control")));
    }

    [Fact]
    public async Task SecurityHeaders_AreSetOnEveryResponse_AndProductionAddsCspAndHsts()
    {
        await using var dev = await AdminHttpHost.StartAsync();
        await using var prod = await AdminHttpHost.StartAsync(production: true);

        var devResponse = await dev.Get("/api/v1/user/probe");
        var prodResponse = await prod.Get("/api/v1/user/probe");

        Assert.Equal("nosniff", devResponse.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("no-referrer", devResponse.Headers.GetValues("Referrer-Policy").Single());
        Assert.False(devResponse.Headers.Contains("Strict-Transport-Security"));
        Assert.Contains("frame-ancestors 'none'", prodResponse.Headers.GetValues("Content-Security-Policy").Single());
        Assert.StartsWith("max-age=31536000", prodResponse.Headers.GetValues("Strict-Transport-Security").Single());
    }

    // ---- startup validation -------------------------------------------------------------------------------

    [Theory]
    [InlineData("ADMIN_COOKIE_SECURE", "false")]
    [InlineData("ADMIN_CSRF_ENABLED", "false")]
    [InlineData("ADMIN_COOKIE_SAME_SITE", "banana")]
    [InlineData("ADMIN_SESSION_IDLE_TIMEOUT", "not-a-time")]
    [InlineData("ADMIN_SESSION_IDLE_TIMEOUT", "00:00:10")]
    [InlineData("ADMIN_SESSION_ABSOLUTE_TIMEOUT", "00:10:00")]
    [InlineData("ADMIN_SESSION_ABSOLUTE_TIMEOUT", "90.00:00:00")]
    [InlineData("ADMIN_LOGIN_RATE_LIMIT", "0")]
    [InlineData("ADMIN_COOKIE_NAME", "bad name;")]
    [InlineData("CORS_ORIGINS", "*")]
    [InlineData("ADMIN_TRUSTED_ORIGINS", "not a url")]
    public void ProductionStartup_RefusesInsecureOrInvalidConfiguration(string key, string value)
    {
        var env = ProductionEnv();
        env[key] = value;

        Assert.Throws<InvalidOperationException>(() =>
            AdminSecuritySettings.Load(true, JwtSecret, name => env.GetValueOrDefault(name)));
    }

    [Fact]
    public void ProductionStartup_WithoutAnyTrustedOrigin_IsRefused()
    {
        var env = ProductionEnv();
        env.Remove("ADMIN_WEB_BASE_URL");

        Assert.Throws<InvalidOperationException>(() =>
            AdminSecuritySettings.Load(true, JwtSecret, name => env.GetValueOrDefault(name)));
    }

    [Fact]
    public void SameSiteNone_IsOnlyAllowedTogetherWithSecure()
    {
        var env = new Dictionary<string, string?> { ["ADMIN_COOKIE_SAME_SITE"] = "None", ["ADMIN_COOKIE_SECURE"] = "false" };

        Assert.Throws<InvalidOperationException>(() =>
            AdminSecuritySettings.Load(false, JwtSecret, name => env.GetValueOrDefault(name)));
    }

    [Fact]
    public void Defaults_AreSecureInProduction()
    {
        var settings = AdminSecuritySettings.Load(true, JwtSecret, name => ProductionEnv().GetValueOrDefault(name));

        Assert.True(settings.CookieSecure);
        Assert.True(settings.CsrfEnabled);
        Assert.Equal(SameSiteMode.Strict, settings.CookieSameSite);
        Assert.Equal("__Host-yakku_admin", settings.CookieName);
        Assert.Equal(TimeSpan.FromMinutes(30), settings.Session.IdleTimeout);
        Assert.Equal(TimeSpan.FromHours(12), settings.Session.AbsoluteTimeout);
        Assert.Equal(32, settings.Session.CsrfKey.Length);
        Assert.Contains(Trusted, settings.TrustedOrigins);
    }

    [Fact]
    public void TheCsrfKey_IsDerived_NotTheJwtSecretItself()
    {
        var settings = AdminSecuritySettings.Load(true, JwtSecret, name => ProductionEnv().GetValueOrDefault(name));

        Assert.NotEqual(Encoding.UTF8.GetBytes(JwtSecret).Take(32), settings.Session.CsrfKey);
    }

    private static Dictionary<string, string?> ProductionEnv() => new() { ["ADMIN_WEB_BASE_URL"] = Trusted };

    // ---- host -----------------------------------------------------------------------------------------------

    private sealed record LoginResult(string Cookie, string Csrf);

    private sealed class AdminHttpHost : IAsyncDisposable
    {
        private readonly WebApplication _app;
        private readonly HttpClient _client;

        private AdminHttpHost(WebApplication app, string baseAddress, AdminSecuritySettings settings)
        {
            _app = app;
            Settings = settings;
            _client = new HttpClient(new HttpClientHandler { UseCookies = false, AllowAutoRedirect = false })
            {
                BaseAddress = new Uri(baseAddress)
            };
            Sessions = app.Services.GetRequiredService<InMemoryAdminSessionRepository>();
            Audit = app.Services.GetRequiredService<RecordingAuditLogWriter>();
            Time = app.Services.GetRequiredService<ManualTimeProvider>();
            SessionService = app.Services.GetRequiredService<IAdminSessionService>();
            Admin = app.Services.GetRequiredService<Admin>();
        }

        public AdminSecuritySettings Settings { get; }
        public InMemoryAdminSessionRepository Sessions { get; }
        public RecordingAuditLogWriter Audit { get; }
        public ManualTimeProvider Time { get; }
        public IAdminSessionService SessionService { get; }
        public Admin Admin { get; }

        public static async Task<AdminHttpHost> StartAsync(bool production = false)
        {
            var env = new Dictionary<string, string?> { ["ADMIN_WEB_BASE_URL"] = Trusted };
            var settings = AdminSecuritySettings.Load(production, JwtSecret, name => env.GetValueOrDefault(name));

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = production ? "Production" : "Development" });
            builder.WebHost.UseUrls("http://127.0.0.1:0");
            builder.Logging.ClearProviders();

            var admin = AdminTestAdmins.Active();
            var sessions = new InMemoryAdminSessionRepository();
            sessions.AddAdmin(admin);

            builder.Services.AddSingleton(admin);
            builder.Services.AddSingleton(sessions);
            builder.Services.AddSingleton<IAdminSessionRepository>(sessions);
            builder.Services.AddSingleton(new ManualTimeProvider());
            builder.Services.AddSingleton<TimeProvider>(sp => sp.GetRequiredService<ManualTimeProvider>());
            builder.Services.AddSingleton(new RecordingAuditLogWriter());
            builder.Services.AddSingleton<IAuditLogWriter>(sp => sp.GetRequiredService<RecordingAuditLogWriter>());
            builder.Services.AddSingleton<IAdminSessionService, AdminSessionService>();

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        ValidateIssuerSigningKey = true,
                        ClockSkew = TimeSpan.Zero,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret))
                    };
                    options.Events = new JwtBearerEvents { OnTokenValidated = AdminSecurityExtensions.RejectAdminBearer };
                });
            builder.Services.AddAdminSecurity(settings);

            var app = builder.Build();
            app.UseMiddleware<SecurityHeadersMiddleware>(!production);
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseMiddleware<AdminRequestGuardMiddleware>();

            // Stand-in for AdminAuthController.Login: the same calls, minus validation and password checks.
            app.MapPost("/api/v1/admin/auth/login", async (
                HttpContext http, IAdminSessionService service, AdminCookieService cookies, AdminCsrfTokens csrf) =>
            {
                var session = await service.CreateAsync(admin.Id, new AdminClientInfo("203.0.113.5", "xunit"));
                cookies.Write(http.Response, session.Token, session.AbsoluteExpiresAt);
                return Results.Ok(new { csrfToken = csrf.Create(session.SessionId) });
            }).AllowAnonymous();

            app.MapPost("/api/v1/admin/auth/logout", async (
                HttpContext http, IAdminSessionService service, AdminCookieService cookies) =>
            {
                var sessionId = Guid.Parse(http.User.FindFirstValue(AuthClaimTypes.SessionId)!);
                await service.RevokeAsync(admin.Id, sessionId, AdminSessionRevocationReasons.Logout);
                cookies.Clear(http.Response);
                return Results.Ok();
            }).RequireAuthorization(AdminSecurityExtensions.AdminPolicy);

            app.MapGet("/api/v1/admin/probe", () => Results.Ok(new { ok = true }))
                .RequireAuthorization(AdminSecurityExtensions.AdminPolicy);
            app.MapPost("/api/v1/admin/probe", () => Results.Ok(new { ok = true }))
                .RequireAuthorization(AdminSecurityExtensions.AdminPolicy);
            app.MapGet("/api/v1/user/probe", () => Results.Ok(new { ok = true })).RequireAuthorization();

            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
            return new AdminHttpHost(app, address, settings);
        }

        public Task<HttpResponseMessage> Get(string path, string? cookie = null, string? bearer = null, string? origin = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, path);
            Decorate(request, cookie, bearer, origin, referer: null, csrf: null);
            return _client.SendAsync(request);
        }

        public Task<HttpResponseMessage> Post(string path, string? cookie, string? csrf, string? origin, string? referer = null)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(new { }) };
            Decorate(request, cookie, bearer: null, origin, referer, csrf);
            return _client.SendAsync(request);
        }

        public Task<HttpResponseMessage> PostLogin(string? origin)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/admin/auth/login") { Content = JsonContent.Create(new { }) };
            Decorate(request, cookie: null, bearer: null, origin, referer: null, csrf: null);
            return _client.SendAsync(request);
        }

        public async Task<LoginResult> LoginAsync(bool production = false)
        {
            var response = await PostLogin(Trusted);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            return new LoginResult(CookieValue(SetCookieOf(response)!), body.GetProperty("csrfToken").GetString()!);
        }

        public string? SetCookieOf(HttpResponseMessage response) =>
            response.Headers.TryGetValues("Set-Cookie", out var values)
                ? values.FirstOrDefault(v => v.StartsWith(Settings.CookieName + "=", StringComparison.Ordinal))
                : null;

        public static string CookieValue(string setCookie) =>
            setCookie[(setCookie.IndexOf('=') + 1)..setCookie.IndexOf(';')];

        public string MakeJwt(Guid subject, bool admin)
        {
            var claims = new List<Claim>
            {
                new(JwtRegisteredClaimNames.Sub, subject.ToString()),
                new(ClaimTypes.NameIdentifier, subject.ToString())
            };
            if (admin)
            {
                claims.Add(new Claim(AuthClaimTypes.Actor, AuthClaimTypes.AdminActor));
            }

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret)), SecurityAlgorithms.HmacSha256));
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private void Decorate(HttpRequestMessage request, string? cookie, string? bearer, string? origin, string? referer, string? csrf)
        {
            if (cookie is not null)
            {
                request.Headers.TryAddWithoutValidation("Cookie", $"{Settings.CookieName}={cookie}");
            }

            if (bearer is not null)
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {bearer}");
            }

            if (origin is not null)
            {
                request.Headers.TryAddWithoutValidation("Origin", origin);
            }

            if (referer is not null)
            {
                request.Headers.TryAddWithoutValidation("Referer", referer);
            }

            if (csrf is not null)
            {
                request.Headers.TryAddWithoutValidation(AdminSecuritySettings.CsrfHeaderName, csrf);
            }
        }

        public async ValueTask DisposeAsync()
        {
            _client.Dispose();
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
