using System.Reflection;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Yakku.API.Controllers;
using Yakku.Application.Auth;
using Yakku.Application.Auth.Sms;
using Yakku.Infrastructure;
using Yakku.Infrastructure.Sms;

namespace Yakku.Tests;

/// <summary>How the SMS service is wired: configuration in, typed HttpClient out, and the endpoint guards.</summary>
public class SmsRegistrationTests
{
    private static ServiceProvider Build(Dictionary<string, string?> environment)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSmsDelivery(name => environment.GetValueOrDefault(name));
        return services.BuildServiceProvider();
    }

    [Fact]
    public void WithCredentials_TheRealMsg91Service_IsRegistered_OnAnHttpsTypedClient()
    {
        using var provider = Build(new()
        {
            ["MSG91_AUTH_KEY"] = "key",
            ["MSG91_FLOW_ID"] = "flow"
        });

        var sms = provider.GetRequiredService<ISmsService>();
        var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(ISmsService));

        Assert.IsType<Msg91SmsService>(sms);
        Assert.True(sms.IsEnabled);
        Assert.Equal("https://control.msg91.com/", client.BaseAddress!.ToString());
        Assert.Equal(Uri.UriSchemeHttps, client.BaseAddress.Scheme);
        Assert.Equal(TimeSpan.FromSeconds(10), client.Timeout);
    }

    [Fact]
    public void TheOtpValidity_ComesFromTheEnvironment_ForTheWholePipeline()
    {
        using var provider = Build(new()
        {
            ["MSG91_AUTH_KEY"] = "key",
            ["MSG91_FLOW_ID"] = "flow",
            ["MSG91_OTP_EXPIRY_MINUTES"] = "7"
        });

        var policy = provider.GetRequiredService<OtpPolicy>();

        Assert.Equal(TimeSpan.FromMinutes(7), policy.Ttl);
        Assert.Equal(7, policy.ExpiryMinutes);
        Assert.Equal(420, policy.ExpiresInSeconds);
    }

    [Fact]
    public void WhenDisabled_NoCredentialsAreNeeded_AndTheServiceReportsItself()
    {
        using var provider = Build(new() { ["MSG91_ENABLED"] = "false" });

        var sms = provider.GetRequiredService<ISmsService>();

        Assert.IsType<DisabledSmsService>(sms);
        Assert.False(sms.IsEnabled);
    }

    [Fact]
    public void WhenEnabledWithoutCredentials_StartupFails_NamingTheVariables()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => Build(new()));

        Assert.Contains("MSG91_AUTH_KEY", exception.Message);
        Assert.Contains("MSG91_FLOW_ID", exception.Message);
    }

    [Fact]
    public void ThereIsNoFallback_ToBuiltInCredentials()
    {
        // Nothing in the environment means nothing in the options: no default key or flow id exists anywhere.
        var options = Msg91Options.FromEnvironment(_ => null);

        Assert.Empty(options.AuthKey);
        Assert.Empty(options.FlowId);
        Assert.NotEmpty(options.Errors);
    }

    [Theory]
    [InlineData(nameof(AuthController.RequestOtp), "otp-send")]
    [InlineData(nameof(AuthController.VerifyOtp), "otp-verify")]
    public void TheOtpEndpoints_AreRateLimitedPerClient(string action, string policy)
    {
        var method = typeof(AuthController).GetMethod(action)!;

        var attribute = method.GetCustomAttribute<EnableRateLimitingAttribute>();

        Assert.NotNull(attribute);
        Assert.Equal(policy, attribute!.PolicyName);
    }
}
