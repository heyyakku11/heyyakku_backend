namespace Yakku.API.Configuration;

internal static class EnvLoader
{
    public static void Load()
    {
        var envPath = FindEnvFile();
        if (envPath is null)
        {
            return;
        }

        foreach (var rawLine in File.ReadAllLines(envPath))
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#'))
            {
                continue;
            }

            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim().Trim('"').Trim('\'');
            if (key.Length == 0)
            {
                continue;
            }

            Environment.SetEnvironmentVariable(key, value);
        }
    }

    public static string GetRequired(string key)
    {
        var value = Environment.GetEnvironmentVariable(key)?.Trim().Trim('"').Trim('\'');
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"{key} is not set. Add it to your .env file or environment variables.");
        }

        return value;
    }

    private static string? FindEnvFile()
    {
        foreach (var start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
        {
            DirectoryInfo? directory;
            try
            {
                directory = new DirectoryInfo(Path.GetFullPath(start));
            }
            catch (Exception)
            {
                continue;
            }

            while (directory is not null)
            {
                var envPath = Path.Combine(directory.FullName, ".env");
                if (File.Exists(envPath))
                {
                    return envPath;
                }

                var backendEnvPath = Path.Combine(directory.FullName, "Backend", ".env");
                if (File.Exists(backendEnvPath))
                {
                    return backendEnvPath;
                }

                directory = directory.Parent;
            }
        }

        return null;
    }
}
