using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Common.Responses;
using Yakku.Application.Users;
using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Services
{
    public class SavedFunCardService : ISavedFunCardService
    {
        private readonly ISavedFunCardRepository _saved;
        private readonly ICheckRepository _checks;

        public SavedFunCardService(ISavedFunCardRepository saved, ICheckRepository checks)
        {
            _saved = saved;
            _checks = checks;
        }

        public async Task<SavedFunCardStateResponse> SaveAsync(
            Guid userId,
            Guid funCardId,
            CancellationToken cancellationToken = default)
        {
            // Only active fun cards can be saved: a weekly check id is a 404 here.
            var card = await _saved.GetActiveFunCardAsync(funCardId, cancellationToken)
                ?? throw CheckExceptions.NotFound();

            if (await _saved.GetAsync(userId, card.Id, cancellationToken) is null)
            {
                await _saved.AddAsync(new SavedFunCard(userId, card.Id), cancellationToken);
                await _saved.SaveChangesAsync(cancellationToken);
            }

            return new SavedFunCardStateResponse { FunCardId = funCardId, Saved = true };
        }

        public async Task<SavedFunCardStateResponse> UnsaveAsync(
            Guid userId,
            Guid funCardId,
            CancellationToken cancellationToken = default)
        {
            var existing = await _saved.GetAsync(userId, funCardId, cancellationToken);
            if (existing is not null)
            {
                _saved.Remove(existing);
                await _saved.SaveChangesAsync(cancellationToken);
            }

            return new SavedFunCardStateResponse { FunCardId = funCardId, Saved = false };
        }

        public async Task<SavedFunCardsPage> ListAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            var decoded = PollCursor.TryDecode(cursor);
            var saves = await _saved.ListByUserAsync(
                userId,
                decoded?.CreatedAt,
                decoded?.Id,
                PollCursor.PageSize + 1,
                cancellationToken);

            var hasMore = saves.Count > PollCursor.PageSize;
            var page = saves.Take(PollCursor.PageSize).ToList();
            var used = await _checks.GetUserChecksByCheckAsync(userId, cancellationToken);
            var nextCursor = hasMore ? PollCursor.Encode(page[^1].SavedAt, page[^1].CheckId) : null;

            return new SavedFunCardsPage
            {
                Items = page.Select(s => FunCardService.ToResponse(s.Check, used.ContainsKey(s.CheckId))).ToList(),
                Meta = PaginationMeta.ForCursor(PollCursor.PageSize, nextCursor, hasMore)
            };
        }
    }
}
