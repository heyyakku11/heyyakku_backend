using System.Security.Cryptography;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Builds the code used in shared poll and check links: 10 random lowercase hexadecimal digits.
    /// Uniqueness is enforced in the database; creators generate a new code when one is taken.
    /// </summary>
    public static class ShareCodeGenerator
    {
        /// <summary>Length of a newly generated code.</summary>
        public const int Length = 10;

        /// <summary>Column size; matches <see cref="Length"/> for new codes.</summary>
        public const int MaxLength = 10;

        public static string New()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(Length / 2)).ToLowerInvariant();
        }
    }
}
