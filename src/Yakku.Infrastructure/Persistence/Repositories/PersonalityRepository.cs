using Microsoft.EntityFrameworkCore;
using Npgsql;
using Yakku.Application.Personality;
using Yakku.Application.Personality.Interfaces;
using Yakku.Domain.Entities;
using Yakku.Infrastructure.Persistence.Configurations;

namespace Yakku.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Personality card persistence. Card changes run in a transaction on a row locked with FOR UPDATE, and the unique
    /// indexes (one card per user, one vote per user per card) are the final protection.
    /// </summary>
    public class PersonalityRepository : IPersonalityRepository
    {
        private const int ShareCodeAttempts = 8;

        private readonly YakkuDbContext _context;

        public PersonalityRepository(YakkuDbContext context)
        {
            _context = context;
        }

        public Task<List<PersonalityTrait>> GetActiveTraitsAsync(CancellationToken cancellationToken = default)
        {
            return _context.PersonalityTraits
                .AsNoTracking()
                .Where(trait => trait.IsActive)
                .OrderBy(trait => trait.DisplayOrder)
                .ThenBy(trait => trait.Id)
                .ToListAsync(cancellationToken);
        }

        public Task<List<PersonalityTrait>> GetTraitsByIdsAsync(
            IReadOnlyCollection<int> ids,
            CancellationToken cancellationToken = default)
        {
            return _context.PersonalityTraits
                .AsNoTracking()
                .Where(trait => ids.Contains(trait.Id))
                .ToListAsync(cancellationToken);
        }

        public async Task EnsureCardAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            // One statement inserts the card and its share code registry row, so neither exists without the other.
            // Selecting from users means a missing user inserts nothing instead of failing the foreign key. The
            // random share code can collide with another code: that is a unique violation, so try a new one.
            for (var attempt = 1; ; attempt++)
            {
                var cardId = Guid.NewGuid();
                var shareCode = ShareCodeGenerator.New();
                try
                {
                    await _context.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        WITH created AS (
                            INSERT INTO personality_cards
                                (id, user_id, share_code, status, required_votes, total_votes, created_at, updated_at)
                            SELECT {cardId}, u."Id", {shareCode}, 'Draft', {PersonalityCard.DefaultRequiredVotes}, 0, now(), now()
                            FROM users u
                            WHERE u."Id" = {userId}
                            ON CONFLICT (user_id) DO NOTHING
                            RETURNING id, share_code
                        )
                        INSERT INTO share_codes (code, content_type, personality_card_id, created_at)
                        SELECT share_code, 'PERSONALITY', id, now() FROM created
                        """,
                        cancellationToken);
                    return;
                }
                catch (PostgresException exception) when (
                    attempt < ShareCodeAttempts
                    && exception.SqlState == PostgresErrorCodes.UniqueViolation
                    && (exception.ConstraintName == PersonalityCardConfiguration.ShareCodeIndex
                        || exception.ConstraintName == "PK_share_codes"))
                {
                    // Collided with another code: loop with a new one.
                }
            }
        }

        public Task<PersonalityCard?> GetCardByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            return _context.PersonalityCards
                .AsNoTracking()
                .Include(card => card.SelfTraits).ThenInclude(item => item.Trait)
                .AsSplitQuery()
                .FirstOrDefaultAsync(card => card.UserId == userId, cancellationToken);
        }

        public Task<PersonalityCard?> GetCardByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default)
        {
            return _context.PersonalityCards
                .AsNoTracking()
                .Include(card => card.User).ThenInclude(user => user.Profile)
                .Include(card => card.SelfTraits).ThenInclude(item => item.Trait)
                .AsSplitQuery()
                .FirstOrDefaultAsync(card => card.ShareCode == shareCode, cancellationToken);
        }

        public Task<bool> HasVotedAsync(Guid cardId, Guid voterUserId, CancellationToken cancellationToken = default)
        {
            return _context.PersonalityVotes
                .AnyAsync(vote => vote.PersonalityCardId == cardId && vote.VoterUserId == voterUserId, cancellationToken);
        }

        public async Task<List<TraitTally>> GetTraitTalliesAsync(Guid cardId, CancellationToken cancellationToken = default)
        {
            var rows = await _context.PersonalityVoteTraits
                .AsNoTracking()
                .Where(item => item.PersonalityVote.PersonalityCardId == cardId)
                .GroupBy(item => new { item.TraitId, item.Trait.Name, item.Trait.DisplayOrder })
                .Select(group => new { group.Key.TraitId, group.Key.Name, group.Key.DisplayOrder, Votes = group.Count() })
                .ToListAsync(cancellationToken);

            return rows
                .Select(row => new TraitTally(row.TraitId, row.Name, row.DisplayOrder, row.Votes))
                .ToList();
        }

        public async Task ExecuteInTransactionAsync(
            Func<CancellationToken, Task> action,
            CancellationToken cancellationToken = default)
        {
            if (_context.Database.CurrentTransaction is not null)
            {
                await action(cancellationToken);
                return;
            }

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            try
            {
                await action(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                _context.ChangeTracker.Clear();
                throw;
            }
        }

        public async Task<PersonalityCard?> LockCardByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var rows = await _context.PersonalityCards
                .FromSqlInterpolated($"SELECT * FROM personality_cards WHERE user_id = {userId} FOR UPDATE")
                .ToListAsync(cancellationToken);
            return rows.Count == 0 ? null : rows[0];
        }

        public async Task<PersonalityCard?> LockCardByShareCodeAsync(string shareCode, CancellationToken cancellationToken = default)
        {
            var rows = await _context.PersonalityCards
                .FromSqlInterpolated($"SELECT * FROM personality_cards WHERE share_code = {shareCode} FOR UPDATE")
                .ToListAsync(cancellationToken);
            return rows.Count == 0 ? null : rows[0];
        }

        public void AddVote(PersonalityVote vote) => _context.PersonalityVotes.Add(vote);

        public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            try
            {
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (
                exception.InnerException is PostgresException postgres
                && postgres.SqlState == PostgresErrorCodes.UniqueViolation
                && string.Equals(postgres.ConstraintName, PersonalityVoteConfiguration.OneVotePerCardIndex, StringComparison.Ordinal))
            {
                throw PersonalityExceptions.AlreadyVoted();
            }
        }
    }
}
