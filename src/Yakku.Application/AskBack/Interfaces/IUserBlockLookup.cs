namespace Yakku.Application.AskBack.Interfaces
{
    /// <summary>
    /// Answers whether one user blocked another from an Ask Back conversation. Voting code uses it to treat the
    /// blocker's cards as closed for the blocked user, without ever saying why.
    /// </summary>
    public interface IUserBlockLookup
    {
        /// <summary>True when <paramref name="blockerUserId"/> blocked <paramref name="blockedUserId"/>.</summary>
        Task<bool> IsBlockedAsync(
            Guid blockerUserId,
            Guid blockedUserId,
            CancellationToken cancellationToken = default);
    }
}
