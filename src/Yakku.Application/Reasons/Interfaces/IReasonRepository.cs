using Yakku.Application.Reasons.DTOs;

namespace Yakku.Application.Reasons.Interfaces
{
    public interface IReasonRepository
    {
        /// <summary>
        /// The reasons for one option of a poll the user owns, newest first, with the number an admin blocked. Null
        /// when the poll is not theirs or the option is not on it. Empty until the results are unlocked, like the
        /// counts. A reason reported but not yet decided is included, flagged; a blocked one is only counted.
        /// </summary>
        Task<ReasonListResponse?> GetPollOptionReasonsAsync(
            Guid pollId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default);

        /// <summary>Same for a made check the user owns (<paramref name="userCheckId"/> is the user_checks id).</summary>
        Task<ReasonListResponse?> GetCheckOptionReasonsAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// The custom answers written in on a poll the user owns, newest first, with their reasons. Null when the poll
        /// is not theirs. Empty until the results are unlocked. The asker's own answer is never included.
        /// </summary>
        Task<CustomAnswerListResponse?> GetPollCustomAnswersAsync(
            Guid pollId,
            Guid ownerId,
            CancellationToken cancellationToken = default);

        Task<CustomAnswerListResponse?> GetCheckCustomAnswersAsync(
            Guid userCheckId,
            Guid ownerId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs a pending report for the reason. The reason stays on the vote until an admin approves the report.
        /// Reporting the same reason again returns the existing report. Null when the poll is not the user's, the
        /// vote does not exist, or its reason has no text (never given, or already blocked).
        /// </summary>
        Task<ReasonReportResponse?> ReportPollReasonAsync(
            Guid pollId,
            Guid ownerId,
            Guid voteId,
            string reporterReason,
            CancellationToken cancellationToken = default);

        Task<ReasonReportResponse?> ReportCheckReasonAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid voteId,
            string reporterReason,
            CancellationToken cancellationToken = default);
    }
}
