using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Mapper;
using Yakku.Application.Users;
using Yakku.Domain.Entities;
using Yakku.Domain.Enums;

namespace Yakku.Application.Polls.Services
{
    public class SavedPollService : ISavedPollService
    {
        private readonly ISavedPollRepository _savedPolls;
        private readonly IPollRepository _polls;

        public SavedPollService(ISavedPollRepository savedPolls, IPollRepository polls)
        {
            _savedPolls = savedPolls;
            _polls = polls;
        }

        public async Task<SavedPollStateResponse> SaveAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            var poll = await _polls.GetByIdAsync(pollId, cancellationToken);
            if (poll is null || poll.Status == PollStatus.Deleted)
            {
                throw NotFound();
            }

            var existing = await _savedPolls.GetByUserAndPollAsync(userId, pollId, cancellationToken);
            if (existing is null)
            {
                await _savedPolls.AddAsync(new SavedPoll(userId, pollId), cancellationToken);
                await _savedPolls.SaveChangesAsync(cancellationToken);
            }

            return new SavedPollStateResponse
            {
                PollId = pollId,
                Saved = true
            };
        }

        public async Task<SavedPollStateResponse> UnsaveAsync(
            Guid userId,
            Guid pollId,
            CancellationToken cancellationToken = default)
        {
            var existing = await _savedPolls.GetByUserAndPollAsync(userId, pollId, cancellationToken);
            if (existing is not null)
            {
                await _savedPolls.DeleteAsync(existing, cancellationToken);
                await _savedPolls.SaveChangesAsync(cancellationToken);
            }

            return new SavedPollStateResponse
            {
                PollId = pollId,
                Saved = false
            };
        }

        public async Task<CreatorPollsPage> ListAsync(
            Guid userId,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            var decoded = PollCursor.TryDecode(cursor);
            var take = PollCursor.PageSize + 1;
            var saves = await _savedPolls.ListByUserAsync(
                userId,
                decoded?.CreatedAt,
                decoded?.Id,
                take,
                cancellationToken);

            var hasMore = saves.Count > PollCursor.PageSize;
            var page = saves.Take(PollCursor.PageSize).ToList();
            string? nextCursor = null;
            if (hasMore)
            {
                var last = page[^1];
                nextCursor = PollCursor.Encode(last.SavedAt, last.PollId);
            }

            return new CreatorPollsPage
            {
                Items = page.Select(saved => saved.Poll.ToResponse()).ToList(),
                Meta = PaginationMeta.ForCursor(PollCursor.PageSize, nextCursor, hasMore)
            };
        }

        private static AppException NotFound()
        {
            return new AppException(
                404,
                ApiErrorCodes.NotFound,
                "Poll not found.",
                "pollId");
        }
    }
}
