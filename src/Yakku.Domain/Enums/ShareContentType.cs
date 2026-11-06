namespace Yakku.Domain.Enums
{
    /// <summary>What a share code resolves to.</summary>
    public enum ShareContentType
    {
        Poll = 0,
        Check = 1,

        /// <summary>A personality card. Opened through the personality card endpoints, not the poll/check share link.</summary>
        Personality = 2
    }
}
