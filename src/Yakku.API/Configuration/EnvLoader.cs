using Yakku.Infrastructure.Configuration;

namespace Yakku.API.Configuration;

internal static class EnvLoader
{
    public const string AspNetCoreEnvironmentVariable = "ASPNETCORE_ENVIRONMENT";
    public const string EnvironmentAliasVariable = "ENVIRONMENT";

    public static void Load()
    {
        EnvFile.Load();
        ApplyEnvironmentName();
    }

    /// <summary>
    /// Lets <c>.env</c> drive which <c>appsettings.{Environment}.json</c> is loaded.
    /// Accepts either <c>ASPNETCORE_ENVIRONMENT</c> or the shorter alias <c>ENVIRONMENT</c>
    /// (<c>Development</c> | <c>Staging</c> | <c>Production</c>).
    /// </summary>
    public static void ApplyEnvironmentName()
    {
        var aspNetCore = Clean(Environment.GetEnvironmentVariable(AspNetCoreEnvironmentVariable));
        var alias = Clean(Environment.GetEnvironmentVariable(EnvironmentAliasVariable));

        var selected = aspNetCore ?? alias;
        if (selected is null)
        {
            return;
        }

        Environment.SetEnvironmentVariable(AspNetCoreEnvironmentVariable, selected);
        if (alias is null)
        {
            Environment.SetEnvironmentVariable(EnvironmentAliasVariable, selected);
        }
    }

    public static string GetRequired(string key)
    {
        return EnvFile.GetRequired(key);
    }

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim().Trim('"').Trim('\'');
        return string.IsNullOrWhiteSpace(trimmed) ? null : trimmed;
    }
}
