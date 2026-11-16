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

            return new ReasonListResponse
            {
                Items = rows.Select(row => new ReasonResponse
                {
                    Id = row.Id,
                    Text = row.Text,
                    CreatedAt = row.CreatedAt,
                    CanAskBack = row.CanAskBack,
                    ReportStatus = StateOf(row.Report)
                }).ToList(),
                BlockedCount = await BlockedCountAsync(ShareContentType.Poll, pollId, optionId, cancellationToken)
            };
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

            return new ReasonListResponse
            {
                Items = rows.Select(row => new ReasonResponse
                {
                    Id = row.Id,
                    Text = row.Text,
                    CreatedAt = row.CreatedAt,
                    CanAskBack = row.CanAskBack,
                    ReportStatus = StateOf(row.Report)
                }).ToList(),
                BlockedCount = await BlockedCountAsync(ShareContentType.Check, userCheckId, optionId, cancellationToken)
            };
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

            return new CustomAnswerListResponse
            {
                Items = rows.Select(ToCustomAnswer).ToList(),
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

            return new CustomAnswerListResponse
            {
                Items = rows.Select(ToCustomAnswer).ToList(),
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

        private static CustomAnswerResponse ToCustomAnswer(CustomAnswerRow row)
        {
            return new CustomAnswerResponse
            {
                Id = row.Id,
                Answer = row.Answer,
                Reason = row.Reason,
                CreatedAt = row.CreatedAt,
                CanAskBack = row.CanAskBack,
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
