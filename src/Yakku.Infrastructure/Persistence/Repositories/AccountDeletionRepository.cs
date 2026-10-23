using Microsoft.EntityFrameworkCore;
using Yakku.Application.Users.Interfaces;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    public class AccountDeletionRepository : IAccountDeletionRepository
    {
        private readonly YakkuDbContext _context;

        public AccountDeletionRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public async Task<IReadOnlyList<Guid>> ListDueUserIdsAsync(
            DateTime utcNow,
            int batchSize,
            CancellationToken cancellationToken = default)
        {
            return await _context.Users
                .AsNoTracking()
                .Where(user => user.DeletionScheduledFor != null && user.DeletionScheduledFor <= utcNow)
                .OrderBy(user => user.DeletionScheduledFor)
                .Select(user => user.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
        }

        public async Task<AccountPurgeResult> PurgeAsync(
            Guid userId,
            DateTime utcNow,
            CancellationToken cancellationToken = default)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                var phoneNumber = await _context.Users
                    .AsNoTracking()
                    .Where(user => user.Id == userId
                                   && user.DeletionScheduledFor != null
                                   && user.DeletionScheduledFor <= utcNow)
                    .Select(user => user.PhoneNumber)
                    .FirstOrDefaultAsync(cancellationToken);

                if (phoneNumber is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new AccountPurgeResult(false, null, Array.Empty<PurgedObject>());
                }

                var objects = await CollectObjectsAsync(userId, cancellationToken);

                // Children first: these foreign keys are Restrict, so they would block the poll and user deletes.
                await _context.PollVotes
                    .Where(vote => vote.VoterId == userId)
                    .ExecuteDeleteAsync(cancellationToken);

                // Check votes no longer have a foreign key to users (guests vote too), so they do not cascade.
                await _context.UserCheckVotes
                    .Where(vote => vote.VoterId == userId)
                    .ExecuteDeleteAsync(cancellationToken);

                await _context.MediaUploadSessions
                    .Where(session => session.UserId == userId
                                      || (session.PollId != null && session.Poll!.CreatorId == userId))
                    .ExecuteDeleteAsync(cancellationToken);

                await _context.Notifications
                    .Where(notification => notification.UserId == userId)
                    .ExecuteDeleteAsync(cancellationToken);

                // Cascades to the polls' options, media, votes and saved entries.
                await _context.Polls
                    .Where(poll => poll.CreatorId == userId)
                    .ExecuteDeleteAsync(cancellationToken);

                // The user's own cascade (profile, preferences, sessions, saved polls, checks and check votes) runs
                // in the database. The due condition is re-checked here so a concurrent login that cancelled the
                // deletion wins: nothing is deleted and the whole transaction rolls back.
                var removed = await _context.Users
                    .Where(user => user.Id == userId
                                   && user.DeletionScheduledFor != null
                                   && user.DeletionScheduledFor <= utcNow)
                    .ExecuteDeleteAsync(cancellationToken);

                if (removed == 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new AccountPurgeResult(false, null, Array.Empty<PurgedObject>());
                }

                await transaction.CommitAsync(cancellationToken);
                return new AccountPurgeResult(true, phoneNumber, objects);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        private async Task<IReadOnlyList<PurgedObject>> CollectObjectsAsync(
            Guid userId,
            CancellationToken cancellationToken)
        {
            var objects = new List<PurgedObject>();

            var media = await _context.PollMedia
                .AsNoTracking()
                .Where(item => item.Poll.CreatorId == userId)
                .Select(item => new { item.BucketName, item.ObjectKey, item.ThumbnailObjectKey })
                .ToListAsync(cancellationToken);

            foreach (var item in media)
            {
                objects.Add(new PurgedObject(item.BucketName, item.ObjectKey));
                if (!string.IsNullOrWhiteSpace(item.ThumbnailObjectKey))
                {
                    objects.Add(new PurgedObject(item.BucketName, item.ThumbnailObjectKey));
                }
            }

            var sessions = await _context.MediaUploadSessions
                .AsNoTracking()
                .Where(session => session.UserId == userId
                                  || (session.PollId != null && session.Poll!.CreatorId == userId))
                .Select(session => new { session.BucketName, session.ObjectKey })
                .ToListAsync(cancellationToken);

            objects.AddRange(sessions
                .Where(session => !string.IsNullOrWhiteSpace(session.ObjectKey))
                .Select(session => new PurgedObject(session.BucketName, session.ObjectKey)));

            return objects.Distinct().ToList();
        }
    }
}
