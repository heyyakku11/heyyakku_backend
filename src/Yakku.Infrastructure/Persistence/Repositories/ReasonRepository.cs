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

        public async Task<List<ReasonResponse>?> GetPollOptionReasonsAsync(
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
                return new List<ReasonResponse>();
            }

            return await _context.PollVotes
                .AsNoTracking()
                .Where(vote => vote.PollId == pollId && vote.VoterId != ownerId && vote.PollOptionId == optionId && vote.Reason != null)
                .OrderByDescending(vote => vote.CreatedAt)
                .ThenByDescending(vote => vote.Id)
                .Select(vote => new ReasonResponse { Id = vote.Id, Text = vote.Reason!, CreatedAt = vote.CreatedAt })
                .ToListAsync(cancellationToken);
        }

        public async Task<List<ReasonResponse>?> GetCheckOptionReasonsAsync(
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
                return new List<ReasonResponse>();
            }

            return await _context.UserCheckVotes
                .AsNoTracking()
                .Where(vote => vote.UserCheckId == userCheckId && vote.VoterId != ownerId && vote.CheckOptionId == optionId && vote.Reason != null)
                .OrderByDescending(vote => vote.CreatedAt)
                .ThenByDescending(vote => vote.Id)
                .Select(vote => new ReasonResponse { Id = vote.Id, Text = vote.Reason!, CreatedAt = vote.CreatedAt })
                .ToListAsync(cancellationToken);
        }

        public async Task<bool> ReportPollReasonAsync(
            Guid pollId,
            Guid ownerId,
            Guid voteId,
            CancellationToken cancellationToken = default)
        {
            var vote = await _context.PollVotes
                .Where(item => item.Id == voteId && item.PollId == pollId && item.Poll.CreatorId == ownerId)
                .FirstOrDefaultAsync(cancellationToken);
            if (vote is null || vote.Reason is null)
            {
                return false;
            }

            _context.ReasonReports.Add(ReasonReport.Create(
                ownerId,
                ShareContentType.Poll,
                pollId,
                vote.Id,
                vote.PollOptionId,
                vote.Reason));
            vote.RemoveReason();
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }

        public async Task<bool> ReportCheckReasonAsync(
            Guid userCheckId,
            Guid ownerId,
            Guid voteId,
            CancellationToken cancellationToken = default)
        {
            var vote = await _context.UserCheckVotes
                .Where(item => item.Id == voteId && item.UserCheckId == userCheckId && item.UserCheck.UserId == ownerId)
                .FirstOrDefaultAsync(cancellationToken);
            if (vote is null || vote.Reason is null)
            {
                return false;
            }

            _context.ReasonReports.Add(ReasonReport.Create(
                ownerId,
                ShareContentType.Check,
                userCheckId,
                vote.Id,
                vote.CheckOptionId,
                vote.Reason));
            vote.RemoveReason();
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
    }
}
