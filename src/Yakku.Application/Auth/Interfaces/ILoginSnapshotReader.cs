using Yakku.Application.Auth.DTOs;

namespace Yakku.Application.Auth.Interfaces
{
    /// <summary>Reads the extra state returned with a sign-in: the user's personality card and device permissions.</summary>
    public interface ILoginSnapshotReader
    {
        /// <summary>
        /// The user's personality card (created now if the account somehow has none) and the notification state of the
        /// installation that signed in. <paramref name="installationId"/> is null when the client sent no device.
        /// </summary>
        Task<LoginSnapshot> GetAsync(
            Guid userId,
            string? installationId,
            CancellationToken cancellationToken = default);
    }
}
