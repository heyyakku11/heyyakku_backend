using Microsoft.EntityFrameworkCore;
using Yakku.Application.Common;
using Yakku.Application.Reasons.DTOs;
using Yakku.Application.Reasons.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class ReasonRepository : IReasonRepository
    {
        private readonly YakkuDbContext _context;

        public ReasonRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<ReasonListResponse?> GetPollOptionReasonsAsync(
            Guid pollId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default)
        {
            var owned = await _context.Polls
                .AsNoTracking()
                .AnyAsync(poll => poll.Id == pollId && poll.CreatorId == ownerId, cancellationToken);
            if (!owned)
            {
                return null;
            }

            var onPoll = await _context.PollOptions
                .AsNoTracking()
                .AnyAsync(option => option.Id == optionId && option.PollId == pollId, cancellationToken);
            if (!onPoll)
            {
                return null;
            }

            // Like the counts, reasons only show once enough other people have answered.
            var otherVotes = await _context.PollVotes
                .AsNoTracking()
                .CountAsync(vote => vote.PollId == pollId && vote.VoterId != ownerId, cancellationToken);
            if (otherVotes < OperationalSettings.Results.MinVotesToReveal)
            {
                return new ReasonListResponse();
            }

            // A reason an admin blocked has no text left on its vote, so it drops out here and is only counted.
            var rows = await _context.PollVotes
                .AsNoTracking()
                .Where(vote => vote.PollId == pollId && vote.VoterId != ownerId && vote.PollOptionId == optionId && vote.Reason != null)
                .OrderByDescending(vote => vote.CreatedAt)
                .ThenByDescending(vote => vote.Id)
                .Select(vote => new
                {
                    vote.Id,
                    Text = vote.Reason!,
                    vote.CreatedAt,
                    CanAskBack = vote.VoterRole == VoterRole.User,
                    Report = _context.ReasonReports
                        .Where(report => report.VoteId == vote.Id)
                        .Select(report => (ReasonReportStatus?)report.Status)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            var lockState = await GetLockStateAsync(ownerId, ShareContentType.Poll, pollId, cancellationToken);
            return ToList(
                ToReasons(rows.Select(row => (row.Id, row.Text, row.CreatedAt, row.CanAskBack, row.Report)), lockState),
                await BlockedCountAsync(ShareContentType.Poll, pollId, optionId, cancellationToken));
        }

        public async Task<ReasonListResponse?> GetCheckOptionReasonsAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid optionId,
            CancellationToken cancellationToken = default)
        {
            var owned = await _context.UserChecks
                .AsNoTracking()
                .Where(item => item.Id == userCheckId && item.UserId == ownerId)
                .Select(item => (Guid?)item.CheckId)
                .FirstOrDefaultAsync(cancellationToken);
            if (owned is null)
            {
                return null;
            }

            var onCheck = await _context.CheckOptions
                .AsNoTracking()
                .AnyAsync(option => option.Id == optionId && option.CheckId == owned.Value, cancellationToken);
            if (!onCheck)
            {
                return null;
            }

            var otherVotes = await _context.UserCheckVotes
                .AsNoTracking()
                .CountAsync(vote => vote.UserCheckId == userCheckId && vote.VoterId != ownerId, cancellationToken);
            if (otherVotes < OperationalSettings.Results.MinVotesToReveal)
            {
                return new ReasonListResponse();
            }

            var rows = await _context.UserCheckVotes
                .AsNoTracking()
                .Where(vote => vote.UserCheckId == userCheckId && vote.VoterId != ownerId && vote.CheckOptionId == optionId && vote.Reason != null)
                .OrderByDescending(vote => vote.CreatedAt)
                .ThenByDescending(vote => vote.Id)
                .Select(vote => new
                {
                    vote.Id,
                    Text = vote.Reason!,
                    vote.CreatedAt,
                    CanAskBack = vote.VoterRole == VoterRole.User,
                    Report = _context.ReasonReports
                        .Where(report => report.VoteId == vote.Id)
                        .Select(report => (ReasonReportStatus?)report.Status)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            var lockState = await GetLockStateAsync(ownerId, ShareContentType.Check, userCheckId, cancellationToken);
            return ToList(
                ToReasons(rows.Select(row => (row.Id, row.Text, row.CreatedAt, row.CanAskBack, row.Report)), lockState),
                await BlockedCountAsync(ShareContentType.Check, userCheckId, optionId, cancellationToken));
        }

        public async Task<CustomAnswerListResponse?> GetPollCustomAnswersAsync(
            Guid pollId,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            var owned = await _context.Polls
                .AsNoTracking()
                .AnyAsync(poll => poll.Id == pollId && poll.CreatorId == ownerId, cancellationToken);
            if (!owned)
            {
                return null;
            }

            var otherVotes = await _context.PollVotes
                .AsNoTracking()
                .CountAsync(vote => vote.PollId == pollId && vote.VoterId != ownerId, cancellationToken);
            if (otherVotes < OperationalSettings.Results.MinVotesToReveal)
            {
                return new CustomAnswerListResponse();
            }

            var rows = await _context.PollVotes
                .AsNoTracking()
                .Where(vote => vote.PollId == pollId && vote.VoterId != ownerId && vote.CustomAnswer != null)
                .OrderByDescending(vote => vote.CreatedAt)
                .ThenByDescending(vote => vote.Id)
                .Select(vote => new CustomAnswerRow(
                    vote.Id,
                    vote.CustomAnswer!,
                    vote.Reason,
                    vote.CreatedAt,
                    vote.VoterRole == VoterRole.User,
                    _context.ReasonReports
                        .Where(report => report.VoteId == vote.Id)
                        .Select(report => (ReasonReportStatus?)report.Status)
                        .FirstOrDefault()))
                .ToListAsync(cancellationToken);

            var lockState = await GetLockStateAsync(ownerId, ShareContentType.Poll, pollId, cancellationToken);
            return new CustomAnswerListResponse
            {
                Items = rows.Select(row => ToCustomAnswer(row, lockState)).ToList(),
                BlockedCount = await BlockedCountAsync(ShareContentType.Poll, pollId, null, cancellationToken)
            };
        }

        public async Task<CustomAnswerListResponse?> GetCheckCustomAnswersAsync(
            Guid userCheckId,
            Guid ownerId,
            CancellationToken cancellationToken = default)
        {
            var owned = await _context.UserChecks
                .AsNoTracking()
                .AnyAsync(item => item.Id == userCheckId && item.UserId == ownerId, cancellationToken);
            if (!owned)
            {
                return null;
            }

            var otherVotes = await _context.UserCheckVotes
                .AsNoTracking()
                .CountAsync(vote => vote.UserCheckId == userCheckId && vote.VoterId != ownerId, cancellationToken);
            if (otherVotes < OperationalSettings.Results.MinVotesToReveal)
            {
                return new CustomAnswerListResponse();
            }

            var rows = await _context.UserCheckVotes
                .AsNoTracking()
                .Where(vote => vote.UserCheckId == userCheckId && vote.VoterId != ownerId && vote.CustomAnswer != null)
                .OrderByDescending(vote => vote.CreatedAt)
                .ThenByDescending(vote => vote.Id)
                .Select(vote => new CustomAnswerRow(
                    vote.Id,
                    vote.CustomAnswer!,
                    vote.Reason,
                    vote.CreatedAt,
                    vote.VoterRole == VoterRole.User,
                    _context.ReasonReports
                        .Where(report => report.VoteId == vote.Id)
                        .Select(report => (ReasonReportStatus?)report.Status)
                        .FirstOrDefault()))
                .ToListAsync(cancellationToken);

            var lockState = await GetLockStateAsync(ownerId, ShareContentType.Check, userCheckId, cancellationToken);
            return new CustomAnswerListResponse
            {
                Items = rows.Select(row => ToCustomAnswer(row, lockState)).ToList(),
                BlockedCount = await BlockedCountAsync(ShareContentType.Check, userCheckId, null, cancellationToken)
            };
        }

        public async Task<ReasonReportResponse?> ReportPollReasonAsync(
            Guid pollId,
            Guid ownerId,
            Guid voteId,
            string reporterReason,
            CancellationToken cancellationToken = default)
        {
            var vote = await _context.PollVotes
                .AsNoTracking()
                .Where(item => item.Id == voteId && item.PollId == pollId && item.Poll.CreatorId == ownerId)
                .Select(item => new { item.PollOptionId, item.Reason })
                .FirstOrDefaultAsync(cancellationToken);
            if (vote is null)
            {
                return null;
            }

            var report = await FindOrLogReportAsync(
                ownerId, ShareContentType.Poll, pollId, voteId, vote.PollOptionId, vote.Reason, reporterReason, cancellationToken);
            if (report is null)
            {
                return null;
            }

            var reasonVotes = _context.PollVotes
                .AsNoTracking()
                .Where(item => item.PollId == pollId && item.VoterId != ownerId && item.Reason != null);
            return new ReasonReportResponse
            {
                ReasonId = voteId,
                OptionId = vote.PollOptionId,
                Status = StateOf(report.Status),
                OptionWhyCount = await reasonVotes.CountAsync(item => item.PollOptionId == vote.PollOptionId, cancellationToken),
                TotalWhyCount = await reasonVotes.CountAsync(cancellationToken)
            };
        }

        public async Task<ReasonReportResponse?> ReportCheckReasonAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid voteId,
            string reporterReason,
            CancellationToken cancellationToken = default)
        {
            var vote = await _context.UserCheckVotes
                .AsNoTracking()
                .Where(item => item.Id == voteId && item.UserCheckId == userCheckId && item.UserCheck.UserId == ownerId)
                .Select(item => new { item.CheckOptionId, item.Reason })
                .FirstOrDefaultAsync(cancellationToken);
            if (vote is null)
            {
                return null;
            }

            var report = await FindOrLogReportAsync(
                ownerId, ShareContentType.Check, userCheckId, voteId, vote.CheckOptionId, vote.Reason, reporterReason, cancellationToken);
            if (report is null)
            {
                return null;
            }

            var reasonVotes = _context.UserCheckVotes
                .AsNoTracking()
                .Where(item => item.UserCheckId == userCheckId && item.VoterId != ownerId && item.Reason != null);
            return new ReasonReportResponse
            {
                ReasonId = voteId,
                OptionId = vote.CheckOptionId,
                Status = StateOf(report.Status),
                OptionWhyCount = await reasonVotes.CountAsync(item => item.CheckOptionId == vote.CheckOptionId, cancellationToken),
                TotalWhyCount = await reasonVotes.CountAsync(cancellationToken)
            };
        }

        /// <summary>
        /// The report for a vote, logging a pending one the first time. The reason stays on the vote: only an admin's
        /// approval removes it. Null when there is nothing to report (no reason text) or an admin already blocked it.
        /// </summary>
        private async Task<ReasonReport?> FindOrLogReportAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            Guid voteId,
            Guid? optionId,
            string? reasonText,
            string reporterReason,
            CancellationToken cancellationToken)
        {
            var existing = await _context.ReasonReports.FirstOrDefaultAsync(item => item.VoteId == voteId, cancellationToken);
            if (existing is not null)
            {
                // Reporting again is a no-op. A blocked reason is gone, so it is no longer found.
                return existing.Status == ReasonReportStatus.Approved ? null : existing;
            }

            if (string.IsNullOrWhiteSpace(reasonText))
            {
                return null;
            }

            var report = ReasonReport.Create(ownerId, contentType, contentId, voteId, optionId, reasonText, reporterReason);
            _context.ReasonReports.Add(report);
            await _context.SaveChangesAsync(cancellationToken);
            return report;
        }

        /// <summary>Reasons an admin blocked for one option, or for the custom answers when <paramref name="optionId"/> is null.</summary>
        private Task<int> BlockedCountAsync(
            ShareContentType contentType,
            Guid contentId,
            Guid? optionId,
            CancellationToken cancellationToken)
        {
            return _context.ReasonReports
                .AsNoTracking()
                .CountAsync(
                    report => report.ContentType == contentType
                        && report.ContentId == contentId
                        && report.OptionId == optionId
                        && report.Status == ReasonReportStatus.Approved,
                    cancellationToken);
        }

        /// <summary>What decides whether asking back on a reason needs an unlock.</summary>
        private sealed record LockState(
            bool CardOpen,
            bool CardHasConversation,
            HashSet<Guid> ConversationVoteIds,
            bool Revealed);

        /// <summary>
        /// The card is open when it was unlocked on its own, unlocked through a plan that is still running, or the
        /// owner's active plan has no ticket limit (asking back unlocks it). Otherwise only a reason that already has
        /// a conversation, or the free top reason while the card has none, can be asked back.
        /// </summary>
        private async Task<LockState> GetLockStateAsync(
            Guid ownerId,
            ShareContentType contentType,
            Guid contentId,
            CancellationToken cancellationToken)
        {
            var now = DateTime.UtcNow;
            var unlockSource = await _context.CardUnlocks
                .AsNoTracking()
                .Where(item => item.OwnerUserId == ownerId && item.ContentType == contentType && item.ContentId == contentId)
                .Select(item => (CardUnlockSource?)item.Source)
                .FirstOrDefaultAsync(cancellationToken);
            var plan = await _context.Users
                .AsNoTracking()
                .Where(user => user.Id == ownerId)
                .Select(user => new { user.PlanId, user.PlanExpiresAt, user.Plan.UnlockTickets })
                .FirstOrDefaultAsync(cancellationToken);
            var planActive = plan is not null
                && plan.PlanId != Plan.FreeId
                && (plan.PlanExpiresAt == null || plan.PlanExpiresAt > now);

            var open = unlockSource == CardUnlockSource.Purchase
                || (unlockSource == CardUnlockSource.Plan && planActive)
                || (planActive && plan!.UnlockTickets == null);

            var voteIds = await _context.AskBackConversations
                .AsNoTracking()
                .Where(item => item.ContentType == contentType && item.ContentId == contentId)
                .Select(item => item.VoteId)
                .ToListAsync(cancellationToken);

            // Reading every reason needs the card bought on its own or an active paid plan. Same rule as
            // CardAccess.ReasonsRevealed: a plan that has ended hides them again (only the free top reason stays).
            var revealed = unlockSource == CardUnlockSource.Purchase || planActive;
            return new LockState(open, voteIds.Count > 0, voteIds.ToHashSet(), revealed);
        }

        private static bool IsLocked(bool canAskBack, bool isTop, Guid voteId, LockState state)
        {
            if (!canAskBack || state.CardOpen || state.ConversationVoteIds.Contains(voteId))
            {
                return false;
            }

            return !(isTop && !state.CardHasConversation);
        }

        /// <summary>
        /// Rows come newest first, so the first registered user's reason is the option's top reason. When the card is
        /// not revealed, only that top reason (or, when every reason is a guest's, the newest one) keeps its text;
        /// the others are hidden: their text is not sent at all.
        /// </summary>
        private static List<ReasonResponse> ToReasons(
            IEnumerable<(Guid Id, string Text, DateTime CreatedAt, bool CanAskBack, ReasonReportStatus? Report)> rows,
            LockState state)
        {
            var list = rows.ToList();
            Guid? freeId = null;
            foreach (var row in list)
            {
                if (row.CanAskBack)
                {
                    freeId = row.Id;
                    break;
                }
            }

            if (freeId is null && list.Count > 0)
            {
                freeId = list[0].Id;
            }

            var items = new List<ReasonResponse>();
            var topSeen = false;
            foreach (var row in list)
            {
                var isTop = row.CanAskBack && !topSeen;
                topSeen |= row.CanAskBack;
                var hidden = !state.Revealed && row.Id != freeId;
                items.Add(new ReasonResponse
                {
                    Id = row.Id,
                    Text = hidden ? string.Empty : row.Text,
                    CreatedAt = row.CreatedAt,
                    CanAskBack = row.CanAskBack,
                    AskBackLocked = IsLocked(row.CanAskBack, isTop, row.Id, state),
                    IsHidden = hidden,
                    ReportStatus = StateOf(row.Report)
                });
            }

            return items;
        }

        private static ReasonListResponse ToList(List<ReasonResponse> items, int blockedCount)
        {
            return new ReasonListResponse
            {
                Items = items,
                HiddenCount = items.Count(item => item.IsHidden),
                BlockedCount = blockedCount
            };
        }

        private static CustomAnswerResponse ToCustomAnswer(CustomAnswerRow row, LockState state)
        {
            return new CustomAnswerResponse
            {
                Id = row.Id,
                Answer = row.Answer,
                Reason = row.Reason,
                CreatedAt = row.CreatedAt,
                CanAskBack = row.CanAskBack,
                AskBackLocked = IsLocked(row.CanAskBack, isTop: true, row.Id, state),
                ReasonReportStatus = row.Reason is null ? ReasonReportState.None : StateOf(row.Report)
            };
        }

        private sealed record CustomAnswerRow(
            Guid Id,
            string Answer,
            string? Reason,
            DateTime CreatedAt,
            bool CanAskBack,
            ReasonReportStatus? Report);

        private static string StateOf(ReasonReportStatus? status)
        {
            return status switch
            {
                ReasonReportStatus.Pending => ReasonReportState.Pending,
                ReasonReportStatus.Rejected => ReasonReportState.Rejected,
                _ => ReasonReportState.None
            };
        }

        private static string StateOf(ReasonReportStatus status)
        {
            return StateOf((ReasonReportStatus?)status);
        }
    }
}
