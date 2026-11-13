using Yakku.Application.Checks;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Checks.Services;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Mapper;
using Yakku.Application.SavedCards.DTOs;
using Yakku.Application.SavedCards.Interfaces;
using Yakku.Application.Users;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.SavedCards.Services
{
    public class SavedCardService : ISavedCardService
    {
        private readonly ISavedCardRepository _saved;
        private readonly IPollRepository _polls;
        private readonly ICheckRepository _checks;

        public SavedCardService(ISavedCardRepository saved, IPollRepository polls, ICheckRepository checks)
        {
            _saved = saved;
            _polls = polls;
            _checks = checks;
        }

        public async Task<SavedCardStateResponse> SaveAsync(
            Guid userId,
            SavedCardType cardType,
            Guid cardId,
            CancellationToken cancellationToken = default)
        {
            var card = await NewCardAsync(userId, cardType, cardId, cancellationToken);

            if (await _saved.GetAsync(userId, cardType, cardId, cancellationToken) is null)
            {
                await _saved.AddAsync(card, cancellationToken);
                await _saved.SaveChangesAsync(cancellationToken);
            }

            return State(cardType, cardId, saved: true);
        }

        public async Task<SavedCardStateResponse> UnsaveAsync(
            Guid userId,
            SavedCardType cardType,
            Guid cardId,
            CancellationToken cancellationToken = default)
        {
            var existing = await _saved.GetAsync(userId, cardType, cardId, cancellationToken);
            if (existing is not null)
            {
                _saved.Remove(existing);
                await _saved.SaveChangesAsync(cancellationToken);
            }

            return State(cardType, cardId, saved: false);
        }

        public async Task<SavedCardsPage> ListAsync(
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

            var pollIds = page.Where(item => item.CardType == SavedCardType.Poll).Select(item => item.PollId!.Value).ToArray();
            var counts = pollIds.Length == 0
                ? new Dictionary<Guid, PollVoteCountSnapshot>()
                : (await _polls.GetVoteCountsAsync(pollIds, cancellationToken)).ToDictionary(pair => pair.Key, pair => pair.Value);
            var made = page.Any(item => item.CardType == SavedCardType.FunCard)
                ? await _checks.GetUserChecksByCheckAsync(userId, cancellationToken)
                : new Dictionary<Guid, UserCheck>();

            var items = page.Select(item => item.CardType == SavedCardType.Poll
                ? new SavedCardResponse
                {
                    CardType = SavedCardTypes.ToApi(item.CardType),
                    CardId = item.PollId!.Value,
                    SavedAt = item.SavedAt,
                    Poll = item.Poll!.ToResponse(counts.GetValueOrDefault(item.PollId!.Value) ?? PollVoteCountSnapshot.Empty)
                }
                : new SavedCardResponse
                {
                    CardType = SavedCardTypes.ToApi(item.CardType),
                    CardId = item.FunCardId!.Value,
                    SavedAt = item.SavedAt,
                    FunCard = FunCardService.ToResponse(item.Check!, made.ContainsKey(item.FunCardId!.Value), saved: true)
                }).ToList();

            string? nextCursor = hasMore ? PollCursor.Encode(page[^1].SavedAt, page[^1].Id) : null;
            return new SavedCardsPage
            {
                Items = items,
                Meta = PaginationMeta.ForCursor(PollCursor.PageSize, nextCursor, hasMore)
            };
        }

        // Validates that the card can be saved by this user and builds the row for it.
        private async Task<SavedCard> NewCardAsync(
            Guid userId,
            SavedCardType cardType,
            Guid cardId,
            CancellationToken cancellationToken)
        {
            if (cardType == SavedCardType.FunCard)
            {
                // Only active fun cards can be saved: a weekly check id is a 404 here.
                var card = await _saved.GetActiveFunCardAsync(cardId, cancellationToken)
                    ?? throw CheckExceptions.NotFound();
                return SavedCard.ForFunCard(userId, card.Id);
            }

            var poll = await _polls.GetByIdAsync(cardId, cancellationToken)
                ?? throw new AppException(404, ApiErrorCodes.NotFound, "Poll not found.", "cardId");

            // Users save other people's polls only, never their own.
            if (poll.CreatorId == userId)
            {
                throw new AppException(400, ApiErrorCodes.ValidationError, "You can't save your own poll.", "cardId");
            }

            return SavedCard.ForPoll(userId, poll.Id);
        }

        private static SavedCardStateResponse State(SavedCardType cardType, Guid cardId, bool saved) => new()
        {
            CardType = SavedCardTypes.ToApi(cardType),
            CardId = cardId,
            Saved = saved
        };
    }
}
