using System.IdentityModel.Tokens.Jwt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yakku.Application.Auth.Services;
using Yakku.Application.Configuration;

namespace Yakku.Tests;

public class JwtLifetimeSettingsTests
{
    [Fact]
    public void Defaults_AreFifteenMinutesAndThirtyDays()
    {
        var settings = Bind();

        Assert.Equal(15, settings.AccessTokenMinutes);
        Assert.Equal(30, settings.RefreshTokenDays);
        Assert.Equal(TimeSpan.FromMinutes(15), settings.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(30), settings.RefreshTokenLifetime);
    }

    [Fact]
    public void DevelopmentOverrides_ShortenAccessAndRefresh()
    {
        var settings = Bind(
            ("Jwt:AccessTokenMinutes", "1"),
            ("Jwt:RefreshTokenDays", "1"));

        Assert.Equal(TimeSpan.FromMinutes(1), settings.AccessTokenLifetime);
        Assert.Equal(TimeSpan.FromDays(1), settings.RefreshTokenLifetime);
    }

    [Fact]
    public void TokenService_UsesConfiguredAccessLifetime()
    {
        var service = new TokenService(
            "jwt-lifetime-test-secret-at-least-32-chars",
            TimeSpan.FromMinutes(1));

        var token = service.CreateAccessToken(Guid.NewGuid(), Guid.NewGuid());
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var lifetime = jwt.ValidTo - jwt.ValidFrom;
        Assert.InRange(lifetime.TotalSeconds, 55, 65);
    }

    private static JwtSettings Bind(params (string Key, string Value)[] values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(pair =>
                new KeyValuePair<string, string?>(pair.Key, pair.Value)))
            .Build();

        var services = new ServiceCollection();
        services.Configure<JwtSettings>(configuration.GetSection(JwtSettings.SectionName));
        return services.BuildServiceProvider().GetRequiredService<IOptions<JwtSettings>>().Value;
    }
}
