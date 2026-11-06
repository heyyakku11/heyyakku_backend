namespace Yakku.Application.Users.Interfaces
{
    public enum AnonymousNameUpdateResult
    {
        Updated,

        /// <summary>Another profile already has that name (case-insensitively).</summary>
        Conflict,

        /// <summary>The user has no profile.</summary>
        ProfileNotFound
    }

    public interface IAnonymousNameRepository
    {
        /// <summary>
        /// Changes the profile's name with one atomic UPDATE on the existing row. The unique index decides a
        /// collision; the old name is left unchanged unless the result is <see cref="AnonymousNameUpdateResult.Updated"/>.
        /// </summary>
        Task<AnonymousNameUpdateResult> TryUpdateAsync(Guid userId, string anonymousName, CancellationToken cancellationToken = default);
    }
}
