using Microsoft.Extensions.Configuration;
using Yakku.Infrastructure.Configuration;

namespace Yakku.Tests;

public class FlatEnvConfigurationBridgeTests
{
    [Fact]
    public void MapsFlatEnvKeys_IntoNestedConfigurationSections()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Msg91:Enabled"] = "false",
                ["Msg91:AuthKey"] = ""
            })
            .AddFlatEnvironmentBridge(name => name switch
            {
                "MSG91_AUTH_KEY" => "secret-key",
                "DB_CONNECTION_STRING" => "Host=localhost",
                "JWT_SECRET" => "a-very-long-jwt-secret-value-here",
                _ => null
            })
            .Build();

        // Enabled is appsettings-only; the bridge never maps MSG91_ENABLED.
        Assert.Equal("false", configuration["Msg91:Enabled"]);
        Assert.Equal("secret-key", configuration["Msg91:AuthKey"]);
        Assert.Equal("Host=localhost", configuration["Database:ConnectionString"]);
        Assert.Equal("a-very-long-jwt-secret-value-here", configuration["Jwt:Secret"]);
    }

    [Fact]
    public void IgnoresEmptyFlatValues_SoAppsettingsDefaultsRemain()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Msg91:Enabled"] = "false",
                ["Storage:Buckets:Image"] = "images"
            })
            .AddFlatEnvironmentBridge(name => name switch
            {
                "POLL_MEDIA_IMAGE_BUCKET" => "",
                _ => null
            })
            .Build();

        Assert.Equal("false", configuration["Msg91:Enabled"]);
        Assert.Equal("images", configuration["Storage:Buckets:Image"]);
    }

}
