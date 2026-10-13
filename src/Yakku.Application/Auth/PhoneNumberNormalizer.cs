namespace Yakku.Application.Auth
{
    public static class PhoneNumberNormalizer
    {
        public static bool TryNormalize(string? value, out string phoneNumber)
        {
            phoneNumber = string.Empty;
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var compact = new string(value.Where(character =>
                character is not (' ' or '-' or '(' or ')')).ToArray());

            if (compact.StartsWith("00", StringComparison.Ordinal))
            {
                compact = "+" + compact[2..];
            }

            if (!compact.StartsWith('+'))
            {
                return false;
            }

            var digits = compact[1..];
            if (digits.Length is < 8 or > 15 || digits.Any(character => !char.IsDigit(character)))
            {
                return false;
            }

            phoneNumber = compact;
            return true;
        }
    }
}
