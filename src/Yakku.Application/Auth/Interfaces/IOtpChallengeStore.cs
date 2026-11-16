using Yakku.Application.Auth.Models;

namespace Yakku.Application.Auth.Interfaces
{
    public interface IOtpChallengeStore
    {
        Task<OtpChallenge?> GetAsync(string email, CancellationToken cancellationToken = default);
        Task SetAsync(string email, OtpChallenge challenge, TimeSpan ttl, CancellationToken cancellationToken = default);
        Task<bool> ReplaceKeepingTtlAsync(string email, OtpChallenge challenge, CancellationToken cancellationToken = default);
        Task DeleteAsync(string email, CancellationToken cancellationToken = default);

        /// <summary>Atomically deletes the challenge only if it is still the one with this id.</summary>
        Task<bool> DeleteIfChallengeMatchesAsync(
            string email,
            Guid challengeId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// One atomic step: refuse while the resend cooldown or the send limit applies, otherwise store the
        /// challenge with the policy's TTL. Concurrent callers cannot both succeed inside the cooldown.
        /// </summary>
        Task<OtpIssueResult> TryIssueAsync(
            string phoneNumber,
            OtpChallenge challenge,
            OtpPolicy policy,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// One atomic step: compare the OTP hash, count a failed attempt, and consume the challenge on a match.
        /// Concurrent verifications cannot both succeed, and parallel guesses cannot dodge the attempt limit.
        /// </summary>
        Task<OtpVerificationResult> VerifyAndConsumeAsync(
            string phoneNumber,
            string otpHash,
            int maxAttempts,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Invalidates a challenge whose SMS was not delivered and gives back its send allowance. Only touches the
        /// challenge with this id, so a newer OTP from a concurrent request is left alone.
        /// </summary>
        Task<bool> RevokeUndeliveredAsync(
            string phoneNumber,
            Guid challengeId,
            CancellationToken cancellationToken = default);
    }
}
