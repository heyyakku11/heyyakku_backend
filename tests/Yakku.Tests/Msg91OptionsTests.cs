using Yakku.Application.Auth;
using Yakku.Infrastructure.Sms;

namespace Yakku.Tests;

/// <summary>Startup validation of the MSG91 settings: fail clearly, never echo a secret, never invent a credential.</summary>
public class Msg91OptionsTests
{
    private static Msg91Options Load(params (string Name, string? Value)[] variables)
    {
        var map = variables.ToDictionary(pair => pair.Name, pair => pair.Value);
        return Msg91Options.FromEnvironment(name => map.GetValueOrDefault(name));
    }

    [Fact]
    public void CompleteConfiguration_IsValid_AndUsesTheDocumentedDefaults()
    {
        var options = Load(
            ("MSG91_AUTH_KEY", "key"),
            ("MSG91_FLOW_ID", "flow"));

        options.ThrowIfInvalid();

        Assert.True(options.Enabled);
        Assert.Equal("key", options.AuthKey);
        Assert.Equal("flow", options.FlowId);
        Assert.Equal(5, options.OtpExpiryMinutes);
        Assert.Equal(TimeSpan.FromMinutes(5), options.OtpPolicy.Ttl);
        Assert.Equal(300, options.OtpPolicy.ExpiresInSeconds);
    }

    [Fact]
    public void Enabled_WithNothingSet_FailsAndNamesEveryMissingVariable()
    {
        var options = Load();

        var exception = Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);

        Assert.Contains("MSG91_AUTH_KEY", exception.Message);
        Assert.Contains("MSG91_FLOW_ID", exception.Message);
        Assert.Contains("MSG91_ENABLED=false", exception.Message);
    }

    [Theory]
    [InlineData("MSG91_AUTH_KEY", "MSG91_FLOW_ID")]
    [InlineData("MSG91_FLOW_ID", "MSG91_AUTH_KEY")]
    public void Enabled_WithOneCredentialMissing_NamesOnlyTheMissingOne(string present, string missing)
    {
        var options = Load((present, "value-that-must-not-appear-in-errors"));

        var exception = Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);

        Assert.Contains(missing, exception.Message);
        Assert.DoesNotContain(present, exception.Message);
        Assert.DoesNotContain("value-that-must-not-appear-in-errors", exception.Message);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"\"")]
    public void BlankCredentials_CountAsMissing(string blank)
    {
        var options = Load(("MSG91_AUTH_KEY", blank), ("MSG91_FLOW_ID", blank));

        Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);
    }

    [Fact]
    public void Disabled_NeedsNoCredentials()
    {
        var options = Load(("MSG91_ENABLED", "false"));

        options.ThrowIfInvalid();

        Assert.False(options.Enabled);
        Assert.Empty(options.AuthKey);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("1", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("0", false)]
    public void TheEnabledFlag_AcceptsTrueAndFalse(string raw, bool expected)
    {
        var options = Load(("MSG91_ENABLED", raw), ("MSG91_AUTH_KEY", "k"), ("MSG91_FLOW_ID", "f"));

        options.ThrowIfInvalid();

        Assert.Equal(expected, options.Enabled);
    }

    [Fact]
    public void ANonsenseEnabledFlag_FailsInsteadOfGuessing()
    {
        var options = Load(("MSG91_ENABLED", "maybe"), ("MSG91_AUTH_KEY", "k"), ("MSG91_FLOW_ID", "f"));

        var exception = Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);

        Assert.Contains("MSG91_ENABLED", exception.Message);
    }

    [Theory]
    [InlineData("1", 1)]
    [InlineData("10", 10)]
    [InlineData("30", 30)]
    public void TheOtpExpiry_IsReadFromTheEnvironment_AndDrivesThePolicy(string raw, int minutes)
    {
        var options = Load(("MSG91_AUTH_KEY", "k"), ("MSG91_FLOW_ID", "f"), ("MSG91_OTP_EXPIRY_MINUTES", raw));

        options.ThrowIfInvalid();

        Assert.Equal(minutes, options.OtpExpiryMinutes);
        Assert.Equal(TimeSpan.FromMinutes(minutes), options.OtpPolicy.Ttl);
        Assert.Equal(minutes, options.OtpPolicy.ExpiryMinutes);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-5")]
    [InlineData("31")]
    [InlineData("five")]
    [InlineData("2.5")]
    public void AnInvalidOtpExpiry_Fails(string raw)
    {
        var options = Load(("MSG91_AUTH_KEY", "k"), ("MSG91_FLOW_ID", "f"), ("MSG91_OTP_EXPIRY_MINUTES", raw));

        var exception = Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);

        Assert.Contains("MSG91_OTP_EXPIRY_MINUTES", exception.Message);
    }

    [Theory]
    [InlineData("YAKKU", true)]
    [InlineData("YAK123", true)]
    [InlineData("TOOLONGSENDERX", false)]
    [InlineData("AB", false)]
    [InlineData("YAK-KU", false)]
    public void TheSenderId_MustBeAShortAlphanumericHeader_WhenGiven(string sender, bool valid)
    {
        var options = Load(("MSG91_AUTH_KEY", "k"), ("MSG91_FLOW_ID", "f"), ("MSG91_SENDER_ID", sender));

        if (valid)
        {
            options.ThrowIfInvalid();
        }
        else
        {
            Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);
        }
    }

    [Fact]
    public void QuotesAndWhitespaceAroundValues_AreTrimmed()
    {
        var options = Load(("MSG91_AUTH_KEY", "  \"quoted-key\"  "), ("MSG91_FLOW_ID", " 'flow-1' "));

        Assert.Equal("quoted-key", options.AuthKey);
        Assert.Equal("flow-1", options.FlowId);
    }

    [Fact]
    public void NeitherToString_NorTheStartupError_RevealsTheAuthKey()
    {
        var options = Load(("MSG91_AUTH_KEY", "super-secret-auth-key"), ("MSG91_ENABLED", "perhaps"));

        Assert.DoesNotContain("super-secret-auth-key", options.ToString());
        var exception = Assert.Throws<InvalidOperationException>(options.ThrowIfInvalid);
        Assert.DoesNotContain("super-secret-auth-key", exception.Message);
    }

    [Fact]
    public void TheOtpPolicy_Defaults_MatchTheExistingOtpSettings()
    {
        Assert.Equal(OtpOptions.Ttl, OtpPolicy.Default.Ttl);
        Assert.Equal(OtpOptions.ResendCooldown, OtpPolicy.Default.ResendCooldown);
        Assert.Equal(OtpOptions.MaxVerificationAttempts, OtpPolicy.Default.MaxVerificationAttempts);
        Assert.Equal(OtpOptions.ExpiresInSeconds, OtpPolicy.Default.ExpiresInSeconds);
    }
}
