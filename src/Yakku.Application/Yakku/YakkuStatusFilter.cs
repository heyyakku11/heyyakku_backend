using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Yakku.Domain.Enums;

namespace Yakku.Application.YakkuDirectory
{
    public static class YakkuStatusFilter
    {
        public static UserStatus? ParseUserStatus(string? status)
        {
            if (IsAll(status))
            {
                return null;
            }

            if (IsActive(status))
            {
                return UserStatus.Active;
            }

            if (IsNonActive(status))
            {
                return UserStatus.Disabled;
            }

            throw Invalid();
        }

        public static bool? ParseGuestActivity(string? status)
        {
            if (IsAll(status))
            {
                return null;
            }

            if (IsActive(status))
            {
                return true;
            }

            if (IsNonActive(status))
            {
                return false;
            }

            throw Invalid();
        }

        public static PollStatus? ParsePollStatus(string? status)
        {
            if (IsAll(status))
            {
                return null;
            }

            return status!.Trim().ToLowerInvariant() switch
            {
                "draft" => PollStatus.Draft,
                "active" => PollStatus.Active,
                "closed" => PollStatus.Closed,
                "deleted" => PollStatus.Deleted,
                _ => throw Invalid()
            };
        }

        private static bool IsAll(string? status)
        {
            return string.IsNullOrWhiteSpace(status)
                || status.Trim().Equals("all", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsActive(string? status)
        {
            return status!.Trim().Equals("active", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsNonActive(string? status)
        {
            return status!.Trim().Equals("nonactive", StringComparison.OrdinalIgnoreCase);
        }

        private static AppException Invalid()
        {
            return new AppException(
                400,
                ApiErrorCodes.ValidationError,
                "Status filter is invalid.",
                "status");
        }
    }
}
