using FluentValidation;
using Yakku.Application.Polls.Mapper;
using Yakku.Application.Polls.DTOs;
using Yakku.Application.Polls.Interfaces;
using Yakku.Application.System;
using Yakku.Application.System.DTOs;
using Yakku.Application.System.Interfaces;
using Yakku.Domain.Enums;
using PollEntity = Yakku.Domain.Entities.Polls;

namespace Yakku.Application.Polls.Services
{
    public class PollService : IPollService
    {
        private readonly IPollRepository _pollRepository;
        private readonly ISystemLogWriter _systemLogWriter;
        private readonly IValidator<CreatePollRequest> _createValidator;
        private readonly IValidator<GetPollQuery> _getByIdValidator;

        public PollService(
            IPollRepository pollRepository,
            ISystemLogWriter systemLogWriter,
            IValidator<CreatePollRequest> createValidator,
            IValidator<GetPollQuery> getByIdValidator)
        {
            _pollRepository = pollRepository;
            _systemLogWriter = systemLogWriter;
            _createValidator = createValidator;
            _getByIdValidator = getByIdValidator;
        }

        public async Task<CreatePollResponse> CreateAsync(
            CreatePollRequest request,
            Guid creatorId,
            CancellationToken cancellationToken = default)
        {
            await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

            var poll = new PollEntity(creatorId, request.Question.Trim(), request.ExpiresAt);

            for (var i = 0; i < request.Options.Count; i++)
            {
                poll.AddOption(request.Options[i].Trim(), i);
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

            return poll.ToCreateResponse();
        }

        public async Task<PollResponse?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken = default)
        {
            var query = new GetPollQuery { Id = id };
            await _getByIdValidator.ValidateAndThrowAsync(query, cancellationToken);

            var poll = await _pollRepository.GetByIdAsync(id, cancellationToken);
            return poll?.ToResponse();
        }
    }
}
