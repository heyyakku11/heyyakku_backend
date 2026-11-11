namespace Yakku.Domain.Enums
{
    /// <summary>How a system-made check is scheduled.</summary>
    public enum CheckKind
    {
        /// <summary>Goes live in a given week (has a live week).</summary>
        Weekly = 0,

        /// <summary>A "just for fun" card: always available while active, no live or expiry date.</summary>
        Fun = 1
    }
}
