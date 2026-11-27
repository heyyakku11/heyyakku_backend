namespace Yakku.Application.Auth.Interfaces
{
    /// <summary>The website's login session: one short-lived JWT, no refresh token.</summary>
    public interface IWebSessionService
    {
        /// <summary>
        /// Starts a website session for the user and returns its access token. The session lasts
        /// <see cref="JwtOptions.WebSessionLifetime"/> and can never be refreshed. It is not counted against the
        /// per-user live session cap and never evicts another session.
        /// </summary>
        Task<string> StartAsync(Guid userId, CancellationToken cancellationToken = default);
    }
}
