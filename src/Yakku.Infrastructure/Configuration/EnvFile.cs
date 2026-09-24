namespace Yakku.Infrastructure.Configuration
{
    public static class EnvFile
    {
        private static string? _loadedEnvPath;

        /// <summary>
        /// Directory that contains the loaded .env file, if any.
        /// Useful for resolving relative paths from env values.
        /// </summary>
        public static string? DirectoryPath =>
            _loadedEnvPath is null ? null : Path.GetDirectoryName(_loadedEnvPath);

        public static void Load()
        {
            var envPath = Find();
            if (envPath is null)
            {
                return;
            }

            _loadedEnvPath = envPath;

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
                if (key.Length == 0 || value.Length == 0)
                {
                    continue;
                }

                Environment.SetEnvironmentVariable(key, value);
            }
        }

        public static string GetRequired(string key)
        {
            Load();

            var value = Environment.GetEnvironmentVariable(key)?.Trim().Trim('"').Trim('\'');
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"{key} is not set. Add it to your .env file or environment variables.");
            }

            return value;
        }

        /// <summary>
        /// Resolves a path that may be relative to the .env file directory or the process CWD.
        /// </summary>
        public static string ResolvePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Path is required.", nameof(path));
            }

            path = path.Trim().Trim('"').Trim('\'');

            if (Path.IsPathRooted(path))
            {
                return Path.GetFullPath(path);
            }

            Load();

            if (DirectoryPath is not null)
            {
                var fromEnv = Path.GetFullPath(Path.Combine(DirectoryPath, path));
                if (File.Exists(fromEnv) || Directory.Exists(fromEnv))
                {
                    return fromEnv;
                }
            }

            return Path.GetFullPath(path);
        }

        private static string? Find()
        {
            foreach (var start in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
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
}
