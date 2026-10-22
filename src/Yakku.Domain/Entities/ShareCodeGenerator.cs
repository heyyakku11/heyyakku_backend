namespace Yakku.Domain.Entities
{
    /// <summary>
    /// Builds the code used in shared poll and check links: the creator's user id (32 hex digits, no dashes)
    /// followed by the creation time in UTC ticks (16 hex digits), 48 lowercase hex characters in all.
    /// </summary>
    public static class ShareCodeGenerator
    {
        public const int MaxLength = 64;

        public static string New(Guid userId, DateTime createdAtUtc)
        {
            return userId.ToString("N") + createdAtUtc.ToUniversalTime().Ticks.ToString("x16");
        }
    }
}
