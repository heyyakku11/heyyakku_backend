using Yakku.Infrastructure.Configuration;

namespace Yakku.API.Configuration;

internal static class EnvLoader
{
    public static void Load()
    {
        EnvFile.Load();
    }

    public static string GetRequired(string key)
    {
        return EnvFile.GetRequired(key);
    }
}
