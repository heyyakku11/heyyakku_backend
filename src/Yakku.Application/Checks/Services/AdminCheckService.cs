using FluentValidation;
using Yakku.Application.Checks.DTOs;
using Yakku.Application.Checks.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Entities;

namespace Yakku.Application.Checks.Services
{
    /// <summary>Admin scheduling of checks: which checks go live in which week.</summary>
    public class AdminCheckService : IAdminCheckService
    {
        private readonly ICheckRepository _repository;
        private readonly IValidator<SaveCheckRequest> _validator;

        public AdminCheckService(ICheckRepository repository, IValidator<SaveCheckRequest> validator)
        {
            _repository = repository;
            _validator = validator;
        }

        public async Task<List<AdminCheckResponse>> ListAsync(
            DateOnly? weekStart,
            CancellationToken cancellationToken = default)
        {
            var week = weekStart is null ? (DateOnly?)null : CheckWeek.StartOf(weekStart.Value);
            var checks = await _repository.GetAllAsync(week, cancellationToken);
            return checks.Select(ToResponse).ToList();
        }

        public async Task<AdminCheckResponse> CreateAsync(
            SaveCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var week = CheckWeek.StartOf(request.WeekStart);
            var sequenceNo = request.SequenceNo ?? await _repository.NextSequenceNoAsync(week, cancellationToken);
            var check = Check.Create(
                week,
                sequenceNo,
                request.Title.Trim(),
                request.Question.Trim(),
                request.BackgroundColor.ToUpperInvariant(),
                request.Sticker.Trim(),
                request.Options.Select(option => option.Trim()));
            if (!request.IsActive)
            {
                check.Update(week, sequenceNo, check.Title, check.Question, check.BackgroundColor, check.Sticker, false);
            }

            await _repository.AddCheckAsync(check, cancellationToken);
            await _repository.SaveChecksAsync(cancellationToken);
            return ToResponse(check);
        }

        public async Task<AdminCheckResponse> UpdateAsync(
            Guid id,
            SaveCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            var check = await _repository.GetForUpdateAsync(id, cancellationToken)
                ?? throw CheckExceptions.NotFound();

            var week = CheckWeek.StartOf(request.WeekStart);
            var weekChanged = week != check.LiveWeekStart;
            var sequenceNo = request.SequenceNo
                ?? (weekChanged ? await _repository.NextSequenceNoAsync(week, cancellationToken) : check.SequenceNo);

            var options = request.Options.Select(option => option.Trim()).ToList();
            var optionsChanged = !check.Options
                .OrderBy(option => option.SortOrder)
                .Select(option => option.Text)
                .SequenceEqual(options);

            // Votes point at options, so the wording can only change while nobody has used the check.
            var used = await _repository.HasUserChecksAsync(id, cancellationToken);
            var contentChanged = optionsChanged ||
                check.Title != request.Title.Trim() ||
                check.Question != request.Question.Trim() ||
                !string.Equals(check.BackgroundColor, request.BackgroundColor, StringComparison.OrdinalIgnoreCase) ||
                check.Sticker != request.Sticker.Trim();
            if (used && contentChanged)
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This check has already been used. Only its week, order and active state can change.");
            }

            if (!used && optionsChanged)
            {
                check.ReplaceOptions(options);
            }

            check.Update(
                week,
                sequenceNo,
                used ? check.Title : request.Title.Trim(),
                used ? check.Question : request.Question.Trim(),
                used ? check.BackgroundColor : request.BackgroundColor.ToUpperInvariant(),
                used ? check.Sticker : request.Sticker.Trim(),
                request.IsActive);

            await _repository.SaveChecksAsync(cancellationToken);
            return ToResponse(check);
        }

        public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
        {
            var check = await _repository.GetForUpdateAsync(id, cancellationToken)
                ?? throw CheckExceptions.NotFound();

            if (await _repository.HasUserChecksAsync(id, cancellationToken))
            {
                throw new AppException(
                    409,
                    ApiErrorCodes.Conflict,
                    "This check has already been used and cannot be deleted. Deactivate it instead.");
            }

            await _repository.RemoveCheckAsync(check, cancellationToken);
            await _repository.SaveChecksAsync(cancellationToken);
        }

        private static AdminCheckResponse ToResponse(Check check)
        {
            return new AdminCheckResponse
            {
                Id = check.Id,
                WeekStart = check.LiveWeekStart,
                SequenceNo = check.SequenceNo,
                Title = check.Title,
                Question = check.Question,
                BackgroundColor = check.BackgroundColor,
                Sticker = check.Sticker,
                IsActive = check.IsActive,
                Status = check.LiveWeekStart <= CheckWeek.Current() ? "live" : "upcoming",
                Options = check.Options
                    .OrderBy(option => option.SortOrder)
                    .Select(option => new CheckOptionResponse { Id = option.Id, Text = option.Text })
                    .ToList()
            };
        }
    }
}
