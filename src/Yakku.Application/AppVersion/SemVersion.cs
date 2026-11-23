namespace Yakku.Application.AppVersion
{
    /// <summary>
    /// Minimal major.minor.patch parser for client version gates. Pre-release and build metadata are ignored.
    /// </summary>
    public static class SemVersion
    {
        public readonly record struct Value(int Major, int Minor, int Patch) : IComparable<Value>
        {
            public int CompareTo(Value other)
            {
                var major = Major.CompareTo(other.Major);
                if (major != 0)
                {
                    return major;
                }

                var minor = Minor.CompareTo(other.Minor);
                return minor != 0 ? minor : Patch.CompareTo(other.Patch);
            }

            public override string ToString() => $"{Major}.{Minor}.{Patch}";
        }

        public static bool TryParse(string? input, out Value version)
        {
            version = default;
            if (string.IsNullOrWhiteSpace(input))
            {
                return false;
            }

            var core = input.Trim();
            var dash = core.IndexOf('-');
            if (dash >= 0)
            {
                core = core[..dash];
            }

            var plus = core.IndexOf('+');
            if (plus >= 0)
            {
                core = core[..plus];
            }

            var parts = core.Split('.');
            if (parts.Length is < 2 or > 3)
            {
                return false;
            }

            if (!int.TryParse(parts[0], out var major) || major < 0
                || !int.TryParse(parts[1], out var minor) || minor < 0)
            {
                return false;
            }

            var patch = 0;
            if (parts.Length == 3)
            {
                if (!int.TryParse(parts[2], out patch) || patch < 0)
                {
                    return false;
                }
            }

            version = new Value(major, minor, patch);
            return true;
        }

        public static int Compare(string left, string right)
        {
            if (!TryParse(left, out var a) || !TryParse(right, out var b))
            {
                throw new ArgumentException("Both versions must be valid semver major.minor[.patch] values.");
            }

            return a.CompareTo(b);
        }
    }
}
