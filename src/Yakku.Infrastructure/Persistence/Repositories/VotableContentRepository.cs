using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls;
using Yakku.Application.Sharing;
using Yakku.Application.Sharing.Interfaces;
using Yakku.Application.Votes;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class VotableContentRepository : IVotableContentRepository
    {
        private const string OneVotePerPollConstraint = "IX_poll_votes_poll_id_voter_id";
        private const string OneVotePerCheckConstraint = "IX_user_check_votes_user_check_id_voter_id";

        private readonly YakkuDbContext _context;

        public VotableContentRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<VotableContent?> FindByShareCodeAsync(
            string shareCode,
            CancellationToken cancellationToken = default)
        {
            var entry = await _context.ShareCodes
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Code == shareCode, cancellationToken);
            if (entry is null)
            {
                return null;
            }

            return entry.ContentType == ShareContentType.Poll
                ? await LoadPollAsync(entry.PollId!.Value, cancellationToken)
                : await LoadCheckAsync(entry.UserCheckId!.Value, cancellationToken);
        }

        public async Task<PollVoteCountSnapshot> GetTallyAsync(
            VotableContent content,
            CancellationToken cancellationToken = default)
        {
            var ownerId = content.OwnerId;
            List<PollVoteCountRow> rows;
            if (content.IsPoll)
            {
                var id = content.ContentId;
                rows = await _context.PollVotes
                    .AsNoTracking()
                    .Where(vote => vote.PollId == id)
                    .GroupBy(vote => vote.PollOptionId)
                    .Select(group => new PollVoteCountRow(
                        id,
                        group.Key,
                        group.Count(),
                        group.Count(vote => vote.Reason != null),
                        group.Count(vote => vote.VoterId != ownerId)))
                    .ToListAsync(cancellationToken);
            }
            else
            {
                var id = content.ContentId;
                rows = await _context.UserCheckVotes
                    .AsNoTracking()
                    .Where(vote => vote.UserCheckId == id)
                    .GroupBy(vote => vote.CheckOptionId)
                    .Select(group => new PollVoteCountRow(
                        id,
                        group.Key,
                        group.Count(),
                        group.Count(vote => vote.Reason != null),
                        group.Count(vote => vote.VoterId != ownerId)))
                    .ToListAsync(cancellationToken);
            }

            return PollVoteCountSnapshot.Combine(rows).GetValueOrDefault(content.ContentId)
                   ?? PollVoteCountSnapshot.Empty;
        }

        public async Task<MyVote?> GetVoteAsync(
            VotableContent content,
            VoterIdentity voter,
            CancellationToken cancellationToken = default)
        {
            // Matches the unique index, which is on (content, voter id): user and guest ids never collide.
            if (content.IsPoll)
            {
                return await _context.PollVotes
                    .AsNoTracking()
                    .Where(vote => vote.PollId == content.ContentId && vote.VoterId == voter.Id)
                    .Select(vote => new MyVote(vote.PollOptionId, vote.CustomAnswer, vote.Reason, vote.CreatedAt))
                    .FirstOrDefaultAsync(cancellationToken);
            }

            return await _context.UserCheckVotes
                .AsNoTracking()
                .Where(vote => vote.UserCheckId == content.ContentId && vote.VoterId == voter.Id)
                .Select(vote => new MyVote(vote.CheckOptionId, vote.CustomAnswer, vote.Reason, vote.CreatedAt))
                .FirstOrDefaultAsync(cancellationToken);
        }

        public async Task<VoteReceipt> AddVoteAsync(
            VotableContent content,
            VoterIdentity voter,
            NewVote vote,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var receipt = content.IsPoll
                    ? await AddPollVoteAsync(content, voter, vote, cancellationToken)
                    : await AddCheckVoteAsync(content, voter, vote, cancellationToken);

                await _context.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return receipt;
            }
            catch (DbUpdateException exception) when (
                IsUniqueViolation(exception, OneVotePerPollConstraint)
                || IsUniqueViolation(exception, OneVotePerCheckConstraint))
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw VoteExceptions.AlreadyVoted();
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        private async Task<VoteReceipt> AddPollVoteAsync(
            VotableContent content,
            VoterIdentity voter,
            NewVote vote,
            CancellationToken cancellationToken)
        {
            var pollId = content.ContentId;

            // FOR SHARE blocks a concurrent close/delete (which update the row) until this vote commits, and makes
            // this vote wait for one that is already in flight, so the state read next is the committed one.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM polls WHERE id = {pollId} FOR SHARE",
                cancellationToken);

            var state = await _context.Polls
                .AsNoTracking()
                .Where(poll => poll.Id == pollId)
                .Select(poll => new { poll.Status, poll.ExpiresAt })
                .FirstOrDefaultAsync(cancellationToken);

            if (state is null)
            {
                throw new AppException(404, ApiErrorCodes.NotFound, "Content not found");
            }

            if (state.Status != PollStatus.Active)
            {
                throw new AppException(400, ApiErrorCodes.ValidationError, "This poll is not accepting votes.");
            }

            if (state.ExpiresAt is not null && state.ExpiresAt <= DateTime.UtcNow)
            {
                throw new AppException(400, ApiErrorCodes.ValidationError, "This poll has expired.");
            }

            var entity = vote.OptionId is Guid optionId
                ? PollVote.ForOption(voter.Id, pollId, optionId, vote.Reason, voter.Role)
                : PollVote.ForCustomAnswer(voter.Id, pollId, vote.CustomAnswer!, vote.Reason, voter.Role);
            _context.PollVotes.Add(entity);
            return new VoteReceipt(entity.Id, entity.CreatedAt);
        }

        private async Task<VoteReceipt> AddCheckVoteAsync(
            VotableContent content,
            VoterIdentity voter,
            NewVote vote,
            CancellationToken cancellationToken)
        {
            var userCheckId = content.ContentId;
            var checkId = content.Check!.CheckId;

            // Admins deactivate a check by updating its row; lock it so a vote cannot slip in after.
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT 1 FROM checks WHERE id = {checkId} FOR SHARE",
                cancellationToken);

            var active = await _context.Checks
                .AsNoTracking()
                .Where(check => check.Id == checkId)
                .Select(check => (bool?)check.IsActive)
                .FirstOrDefaultAsync(cancellationToken);

            if (active is null)
            {
                throw new AppException(404, ApiErrorCodes.NotFound, "Content not found");
            }

            if (active == false)
            {
                throw new AppException(400, ApiErrorCodes.ValidationError, "This check is not accepting votes.");
            }

            var entity = vote.OptionId is Guid optionId
                ? UserCheckVote.ForOption(userCheckId, checkId, optionId, voter.Id, vote.Reason, voter.Role)
                : UserCheckVote.ForCustomAnswer(userCheckId, checkId, vote.CustomAnswer!, voter.Id, vote.Reason, voter.Role);
            _context.UserCheckVotes.Add(entity);
            return new VoteReceipt(entity.Id, entity.CreatedAt);
        }

        private async Task<VotableContent?> LoadPollAsync(Guid pollId, CancellationToken cancellationToken)
        {
            var poll = await _context.Polls
                .AsNoTracking()
                .Include(item => item.Options)
                .Include(item => item.Media)
                .FirstOrDefaultAsync(item => item.Id == pollId, cancellationToken);
            if (poll is null)
            {
                return null;
            }

            return new VotableContent
            {
                ContentType = ShareContentType.Poll,
                ContentId = poll.Id,
                ShareCode = poll.ShareCode,
                OwnerId = poll.CreatorId,
                Question = poll.Question,
                Status = poll.Status.ToString().ToUpperInvariant(),
                IsOpen = poll.Status == PollStatus.Active,
                PubliclyReadable = poll.Status == PollStatus.Active,
                ExpiresAt = poll.ExpiresAt,
                CreatedAt = poll.CreatedAt,
                Options = poll.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new VotableOption(option.Id, option.Text, option.SortOrder))
                    .ToList(),
                Poll = new VotablePollDetails(poll.ContentType.ToString().ToUpperInvariant(), poll.Media)
            };
        }

        private async Task<VotableContent?> LoadCheckAsync(Guid userCheckId, CancellationToken cancellationToken)
        {
            var item = await _context.UserChecks
                .AsNoTracking()
                .Include(userCheck => userCheck.Check)
                .ThenInclude(check => check.Options)
                .FirstOrDefaultAsync(userCheck => userCheck.Id == userCheckId, cancellationToken);
            if (item is null)
            {
                return null;
            }

            return new VotableContent
            {
                ContentType = ShareContentType.Check,
                ContentId = item.Id,
                ShareCode = item.ShareCode,
                OwnerId = item.UserId,
                Question = item.Check.Question,
                Status = item.Check.IsActive ? "ACTIVE" : "INACTIVE",
                IsOpen = item.Check.IsActive,
                PubliclyReadable = true,
                CreatedAt = item.CreatedAt,
                Options = item.Check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new VotableOption(option.Id, option.Text, option.SortOrder))
                    .ToList(),
                Check = new VotableCheckDetails(
                    item.Check.Id,
                    item.Check.Title,
                    item.Check.BackgroundColor,
                    item.Check.Sticker)
            };
        }

        private static bool IsUniqueViolation(DbUpdateException exception, string constraintName)
        {
            return exception.InnerException is PostgresException postgres &&
                   postgres.SqlState == PostgresErrorCodes.UniqueViolation &&
                   string.Equals(postgres.ConstraintName, constraintName, StringComparison.Ordinal);
        }
    }
}
