namespace Yakku.Application.Users.Interfaces
{
    /// <summary>Finds an anonymous name that is free in the database and in the reservation store, and holds it.</summary>
    public interface IAnonymousNameAllocator
    {
        /// <summary>
        /// Returns a generated name that no profile uses (case-insensitively) and nobody else holds, and holds it
        /// for <paramref name="holdFor"/>. Throws a 503 <c>ANONYMOUS_NAME_UNAVAILABLE</c> when the retry limit is reached.
        /// </summary>
        Task<string> ReserveAsync(TimeSpan holdFor, CancellationToken cancellationToken = default);

        Task RefreshAsync(string name, TimeSpan holdFor, CancellationToken cancellationToken = default);

        Task ReleaseAsync(string name, CancellationToken cancellationToken = default);
    }
}
