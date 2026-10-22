namespace Yakku.Application.Users.Interfaces
{
    public sealed record PurgedObject(string BucketName, string ObjectKey);

    /// <summary>Outcome of purging one account. <see cref="Deleted"/> is false when it was no longer due.</summary>
    public sealed record AccountPurgeResult(
        bool Deleted,
        string? PhoneNumber,
        IReadOnlyList<PurgedObject> Objects);

    public interface IAccountDeletionRepository
    {
        Task<IReadOnlyList<Guid>> ListDueUserIdsAsync(
            DateTime utcNow,
            int batchSize,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Atomically removes the user and everything they own (polls, votes, notifications, upload sessions,
        /// profile, sessions, checks), honouring the Restrict foreign keys. Does nothing if the user is no longer
        /// due, for example because they logged in again. Returns the stored objects to delete once it committed.
        /// </summary>
        Task<AccountPurgeResult> PurgeAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default);
    }
}
