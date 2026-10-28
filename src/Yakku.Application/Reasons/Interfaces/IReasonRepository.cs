using Yakku.Application.Reasons.DTOs;

namespace Yakku.Application.Reasons.Interfaces
{
    public interface IReasonRepository
    {
        /// <summary>
        /// The reasons for one option of a poll the user owns, newest first. Null when the poll is not theirs or the
        /// option is not on it. Empty until the results are unlocked, like the counts.
        /// </summary>
        Task<List<ReasonResponse>?> GetPollOptionReasonsAsync(
            Guid pollId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default);

        /// <summary>Same for a made check the user owns (<paramref name="userCheckId"/> is the user_checks id).</summary>
        Task<List<ReasonResponse>?> GetCheckOptionReasonsAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Logs the report and removes the reason from the vote, in one save, and returns the reason counts as they
        /// are now. Null when the poll is not the user's or the vote has no reason (reporting an already removed
        /// reason is not found).
        /// </summary>
        Task<ReasonReportResponse?> ReportPollReasonAsync(
            Guid pollId,
            Guid ownerId,
            Guid voteId,
            CancellationToken cancellationToken = default);

        Task<ReasonReportResponse?> ReportCheckReasonAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid voteId,
            CancellationToken cancellationToken = default);
    }
}
