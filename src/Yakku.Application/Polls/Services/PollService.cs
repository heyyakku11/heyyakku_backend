using FluentValidation;
using Yakku.Application.Categories.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Application.Images.Interfaces;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.Polls.Mapper;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Application.Users;
using Yakku.Domain.Enums;
using PollEntity = Yakku.Domain.Entities.Polls;

namespace Yakku.Application.Polls.Services
{
    public class PollService : IPollService
    {
        private readonly IPollRepository _pollRepository;
        private readonly ICategoryRepository _categoryRepository;
        private readonly IImageRepository _imageRepository;
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<CreatePollRequest> _createValidator;

        public PollService(
            IPollRepository pollRepository,
            ICategoryRepository categoryRepository,
            IImageRepository imageRepository,
            ISystemLogWriter systemLogWriter,
            IValidator<CreatePollRequest> createValidator)
        {
            _pollRepository = pollRepository;
            _categoryRepository = categoryRepository;
            _imageRepository = imageRepository;
            _systemLogWriter = systemLogWriter;
            _createValidator = createValidator;
        }

        public async Task<PollResponse> CreateAsync(
            CreatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

            var optionType = Enum.Parse<OptionType>(request.OptionType.Trim(), ignoreCase: true);

            if (request.CategoryId is not null)
            {
                var category = await _categoryRepository.GetActiveByIdAsync(
                    request.CategoryId.Value,
                    cancellationToken);
                if (category is null)
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "Category is invalid or inactive.",
                        "categoryId");
                }
            }

            if (optionType == OptionType.Image)
            {
                var imageIds = request.Options
                    .Select(option => option.ImageId!.Value)
                    .Distinct()
                    .ToList();
                var existing = await _imageRepository.GetExistingIdsAsync(imageIds, cancellationToken);
                var missing = imageIds.Where(id => !existing.Contains(id)).ToList();
                if (missing.Count > 0)
                {
                    throw new AppException(
                        400,
                        ApiErrorCodes.ValidationError,
                        "One or more image IDs are invalid.",
                        "options",
                        $"Missing image IDs: {string.Join(", ", missing)}");
                }
            }

            var poll = new PollEntity(
                creatorId,
                request.Question.Trim(),
                optionType,
                request.CategoryId,
                request.ExpiresAt);

            for (var i = 0; i < request.Options.Count; i++)
            {
                var sortOrder = i + 1;
                var option = request.Options[i];
                if (optionType == OptionType.Text)
                {
                    poll.AddTextOption(option.Text!.Trim(), sortOrder);
                }
                else
                {
                    poll.AddImageOption(option.ImageId!.Value, sortOrder);
                }
            }

            await _pollRepository.AddAsync(poll, cancellationToken);
            await _pollRepository.SaveChangesAsync(cancellationToken);
            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.PollCreated,
                    Message = "Poll created.",
                    UserId = creatorId,
                    Details = new { pollId = poll.Id }
                },
                cancellationToken);

            return poll.ToResponse();
        }

        public async Task<CreatorPollsPage> GetCreatorPollsAsync(
            Guid creatorId,
            string? cursor,
            CancellationToken cancellationToken = default)
        {
            var decoded = PollCursor.TryDecode(cursor);
            var take = PollCursor.PageSize + 1;
            var polls = await _pollRepository.GetCreatedByUserAsync(
                creatorId,
                decoded?.CreatedAt,
                decoded?.Id,
                take,
                cancellationToken);

            var hasMore = polls.Count > PollCursor.PageSize;
            var items = polls
                .Take(PollCursor.PageSize)
                .Select(poll => poll.ToSummaryResponse())
                .ToList();

            string? nextCursor = null;
            if (hasMore)
            {
                var last = polls[PollCursor.PageSize - 1];
                nextCursor = PollCursor.Encode(last.CreatedAt, last.Id);
            }

            return new CreatorPollsPage
            {
                Items = items,
                Meta = PaginationMeta.ForCursor(PollCursor.PageSize, nextCursor, hasMore)
            };
        }

        public async Task<PollResponse> GetPollDetailsAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            var poll = await GetOwnedVisiblePollAsync(pollId, creatorId, cancellationToken);
            return poll.ToResponse();
        }

        public async Task<PollResponse> ClosePollAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            var poll = await GetOwnedVisiblePollAsync(pollId, creatorId, cancellationToken);

            if (poll.Status == PollStatus.Closed)
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "Poll is already closed.");
            }

            if (!poll.TryClose())
            {
                throw new AppException(
                    400,
                    ApiErrorCodes.ValidationError,
                    "Poll cannot be closed in its current state.");
            }

            await _pollRepository.SaveChangesAsync(cancellationToken);
            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.PollClosed,
                    Message = "Poll closed.",
                    UserId = creatorId,
                    Details = new { pollId = poll.Id }
                },
                cancellationToken);

            return poll.ToResponse();
        }

        public async Task DeletePollAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            var poll = await GetOwnedVisiblePollAsync(pollId, creatorId, cancellationToken);

            if (!poll.TrySoftDelete())
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "Poll not found");
            }

            await _pollRepository.SaveChangesAsync(cancellationToken);
            await _systemLogWriter.WriteAsync(
                new SystemLogWriteRequest
                {
                    Level = SystemLogLevel.Information,
                    EventType = SystemLogEventTypes.PollDeleted,
                    Message = "Poll deleted.",
                    UserId = creatorId,
                    Details = new { pollId = poll.Id }
                },
                cancellationToken);
        }

        private async Task<PollEntity> GetOwnedVisiblePollAsync(
            Guid pollId,
            Guid creatorId,
            CancellationToken cancellationToken)
        {
            var poll = await _pollRepository.GetByIdAndCreatorAsync(pollId, creatorId, cancellationToken);
            if (poll is null || poll.Status == PollStatus.Deleted)
            {
                throw new AppException(
                    404,
                    ApiErrorCodes.NotFound,
                    "Poll not found");
            }

            return poll;
        }
    }
}
