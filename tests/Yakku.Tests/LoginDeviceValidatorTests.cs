using Yakku.Application.Auth.DTOs;
using Yakku.Application.Auth.Validators;

namespace Yakku.Tests;

public class LoginDeviceValidatorTests
{
    private static VerifyOtpRequest Request(LoginDeviceRequest? device) => new()
    {
        PhoneNumber = "+15551234567",
        Otp = "123456",
        Device = device
    };

    [Fact]
    public void TheDevicePayload_IsOptional_AndNeedsOnlyTheInstallationIdAndPlatform()
    {
        var validator = new VerifyOtpValidator();

        Assert.True(validator.Validate(Request(null)).IsValid);
        Assert.True(validator.Validate(Request(new LoginDeviceRequest
        {
            InstallationId = "install-1",
            Platform = "android"
        })).IsValid);
    }

    [Theory]
    [InlineData("", "android")]
    [InlineData("has space", "android")]
    [InlineData("install-1", "")]
    [InlineData("install-1", "windows")]
    public void ABadInstallationOrPlatform_IsRejected(string installationId, string platform)
    {
        var result = new VerifyOtpValidator().Validate(Request(new LoginDeviceRequest
        {
            InstallationId = installationId,
            Platform = platform
        }));

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ThePayloadHasNoPushTokenOrPermissionFields()
    {
        var properties = typeof(LoginDeviceRequest).GetProperties().Select(p => p.Name).Order();

        Assert.Equal(["InstallationId", "Platform"], properties);
    }
}
