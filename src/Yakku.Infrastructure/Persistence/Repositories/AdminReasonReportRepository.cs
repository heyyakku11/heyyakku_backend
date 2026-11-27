using Microsoft.EntityFrameworkCore;
using Yakku.Application.AdminReasonReports.DTOs;
using Yakku.Application.AdminReasonReports.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class AdminReasonReportRepository : IAdminReasonReportRepository
    {
        private readonly YakkuDbContext _context;

        public AdminReasonReportRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<AdminReasonReportPage> GetReportsAsync(
            ReasonReportStatus? status, int page, int pageSize, CancellationToken cancellationToken = default)
        {
            var query = _context.ReasonReports.AsNoTracking();
            if (status is not null)
            {
                query = query.Where(report => report.Status == status);
            }

            var total = await query.CountAsync(cancellationToken);
            var rows = await query
                .OrderByDescending(report => report.CreatedAt)
                .ThenBy(report => report.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(report => new
                {
                    report.Id,
                    report.ContentType,
                    report.ContentId,
                    report.VoteId,
                    report.OptionId,
                    report.ReasonText,
                    report.ReporterReason,
                    report.Status,
                    report.CreatedAt,
                    report.ReviewedAt,
                    ReporterAnonymousName = _context.Users
                        .Where(user => user.Id == report.ReporterId)
                        .Select(user => user.AnonymousName)
                        .FirstOrDefault()
                })
                .ToListAsync(cancellationToken);

            return new AdminReasonReportPage
            {
                TotalCount = total,
                Items = rows.Select(row => new AdminReasonReportResponse
                {
                    Id = row.Id,
                    ContentType = row.ContentType.ToString().ToLowerInvariant(),
                    ContentId = row.ContentId,
                    VoteId = row.VoteId,
                    OptionId = row.OptionId,
                    ReasonText = row.ReasonText,
                    ReporterReason = row.ReporterReason,
                    Status = row.Status.ToString().ToLowerInvariant(),
                    ReporterAnonymousName = row.ReporterAnonymousName ?? string.Empty,
                    CreatedAt = row.CreatedAt,
                    ReviewedAt = row.ReviewedAt
                }).ToList()
            };
        }

        public async Task<ReasonReportDecisionResult> ApproveAsync(
            Guid reportId, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var report = await _context.ReasonReports.FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);
            if (report is null)
            {
                return ReasonReportDecisionResult.NotFound;
            }

            if (!report.Approve(utcNow))
            {
                return ReasonReportDecisionResult.AlreadyDecided;
            }

            // The reason text leaves the vote: every list and count reads it from there.
            if (report.ContentType == ShareContentType.Poll)
            {
                var vote = await _context.PollVotes.FirstOrDefaultAsync(item => item.Id == report.VoteId, cancellationToken);
                vote?.RemoveReason();
            }
            else
            {
                var vote = await _context.UserCheckVotes.FirstOrDefaultAsync(item => item.Id == report.VoteId, cancellationToken);
                vote?.RemoveReason();
            }

            await _context.SaveChangesAsync(cancellationToken);
            await CloseAskBackAsync(report.VoteId, utcNow, cancellationToken);
            return ReasonReportDecisionResult.Decided;
        }

        public async Task<ReasonReportDecisionResult> RejectAsync(
            Guid reportId, DateTime utcNow, CancellationToken cancellationToken = default)
        {
            var report = await _context.ReasonReports.FirstOrDefaultAsync(item => item.Id == reportId, cancellationToken);
            if (report is null)
            {
                return ReasonReportDecisionResult.NotFound;
            }

            if (!report.Reject(utcNow))
            {
                return ReasonReportDecisionResult.AlreadyDecided;
            }

            await _context.SaveChangesAsync(cancellationToken);
            return ReasonReportDecisionResult.Decided;
        }

        /// <summary>
        /// A blocked reason ends the Ask Back conversation about it: the reason the asker rejected is gone, and the
        /// voter should not be left able to keep writing to them. The asker can reopen it.
        /// </summary>
        private Task<int> CloseAskBackAsync(Guid voteId, DateTime now, CancellationToken cancellationToken)
        {
            return _context.AskBackConversations
                .Where(item => item.VoteId == voteId && item.Status == AskBackStatus.Active)
                .ExecuteUpdateAsync(
                    set => set
                        .SetProperty(item => item.Status, AskBackStatus.Closed)
                        .SetProperty(item => item.ClosedAt, (DateTime?)now),
                    cancellationToken);
        }
    }
}
