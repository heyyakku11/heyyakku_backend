using System.Security.Cryptography;

namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Builds the code used in shared poll and check links: 6 random lowercase hexadecimal digits (for example
    /// <c>a3f09c</c>). Uniqueness is not by construction: the code is unique and not null in the database
    /// (polls, user_checks and the share_codes registry), and creators generate a new code when one is taken.
    /// Codes created before this format were longer (up to <see cref="MaxLength"/> characters) and stay valid.
    /// </summary>
    public static class ShareCodeGenerator
    {
        /// <summary>Length of a newly generated code.</summary>
        public const int Length = 6;

        /// <summary>Column size. Larger than <see cref="Length"/> so the older, longer codes keep resolving.</summary>
        public const int MaxLength = 64;

        public static string New()
        {
            return Convert.ToHexString(RandomNumberGenerator.GetBytes(Length / 2)).ToLowerInvariant();
        }
    }
}
