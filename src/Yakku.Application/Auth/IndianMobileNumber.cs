namespace Yakku.Application.Auth
{
    /// <summary>
    /// The SMS provider only reaches Indian mobiles (DLT-registered template), and the app always signs in with
    /// +91 and ten digits. Indian mobile numbers start with 6, 7, 8 or 9.
    /// </summary>
    public static class IndianMobileNumber
    {
        private const string CountryPrefix = "+91";
        private const int NationalLength = 10;

        /// <summary>True for a normalised E.164 number of the form +91 followed by a ten-digit mobile.</summary>
        public static bool IsValid(string? e164)
        {
            return TryFormatForMsg91(e164, out _);
        }

        /// <summary>MSG91 wants the country code and the number without a plus sign: 919876543210.</summary>
        public static bool TryFormatForMsg91(string? e164, out string mobiles)
        {
            mobiles = string.Empty;
            if (e164 is null
                || !e164.StartsWith(CountryPrefix, StringComparison.Ordinal)
                || e164.Length != CountryPrefix.Length + NationalLength)
            {
                return false;
            }

            var national = e164[CountryPrefix.Length..];
            if (national[0] is < '6' or > '9' || national.Any(character => character is < '0' or > '9'))
            {
                return false;
            }

            mobiles = "91" + national;
            return true;
        }
    }
}
