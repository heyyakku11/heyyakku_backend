using Yakku.Application.AdminUsers.DTOs;
using Yakku.Application.AdminUsers.Interfaces;
using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Enums;

namespace Yakku.Application.AdminUsers.Services
{
    public class AdminUserService : IAdminUserService
    {
        public const int DefaultPageSize = 10;
        public const int MaxPageSize = 50;

        private readonly IAdminUserRepository _repository;
        private readonly TimeProvider _time;

        public AdminUserService(IAdminUserRepository repository, TimeProvider time)
        {
            _repository = repository;
            _time = time;
        }

        public async Task<(List<AdminUserResponse> Items, PaginationMeta Meta)> ListUsersAsync(
            string? status, string? cursor, int pageSize, CancellationToken cancellationToken = default)
        {
            UserStatus? filter = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                // Enum.TryParse also accepts numbers; only the names are valid here.
                if (!Enum.TryParse<UserStatus>(status.Trim(), ignoreCase: true, out var parsed)
                    || !Enum.IsDefined(parsed)
                    || char.IsDigit(status.Trim()[0]))
                {
                    throw Invalid("Status must be active or disabled.");
                }

                filter = parsed;
            }

            var after = AdminUserCursor.TryDecode(cursor);
            pageSize = NormalizePageSize(pageSize);
            var result = await _repository.GetUsersAsync(filter, after, pageSize, cancellationToken);
            var last = result.Items.LastOrDefault();
            var next = result.HasMore && last is not null ? AdminUserCursor.Encode(last.CreatedAt, last.Id) : null;
            return (result.Items, PaginationMeta.ForCursor(pageSize, next, result.HasMore));
        }

        public async Task<(List<AdminGuestResponse> Items, PaginationMeta Meta)> ListGuestsAsync(
            string? status, string? cursor, int pageSize, CancellationToken cancellationToken = default)
        {
            bool? activeOnly = null;
            if (!string.IsNullOrWhiteSpace(status))
            {
                activeOnly = status.Trim().ToLowerInvariant() switch
                {
                    "active" => true,
                    "expired" => false,
                    _ => throw Invalid("Status must be active or expired.")
                };
            }

            var after = AdminUserCursor.TryDecode(cursor);
            pageSize = NormalizePageSize(pageSize);
            var result = await _repository.GetGuestsAsync(
                activeOnly, _time.GetUtcNow().UtcDateTime, after, pageSize, cancellationToken);
            var last = result.Items.LastOrDefault();
            var next = result.HasMore && last is not null ? AdminUserCursor.Encode(last.CreatedAt, last.Id) : null;
            return (result.Items, PaginationMeta.ForCursor(pageSize, next, result.HasMore));
        }

        private static int NormalizePageSize(int pageSize)
        {
            return pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);
        }

        private static AppException Invalid(string message)
        {
            return new AppException(400, ApiErrorCodes.ValidationError, message);
        }
    }
}
