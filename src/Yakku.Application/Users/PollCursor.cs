using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Encoding = System.Text.Encoding;

namespace Yakku.Application.Users
{
    internal static class PollCursor
    {
        public const int PageSize = 10;

        public static string Encode(DateTime createdAt, Guid id)
        {
            var payload = $"{createdAt.Ticks}:{id:D}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        }

        public static (DateTime CreatedAt, Guid Id)? TryDecode(string? cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                return null;
            }

            try
            {
                var payload = Encoding.UTF8.GetString(Convert.FromBase64String(cursor.Trim()));
                var separator = payload.IndexOf(':');
                if (separator <= 0)
                {
                    throw InvalidCursor();
                }

                if (!long.TryParse(payload[..separator], out var ticks) ||
                    !Guid.TryParse(payload[(separator + 1)..], out var id) ||
                    id == Guid.Empty)
                {
                    throw InvalidCursor();
                }

                return (new DateTime(ticks, DateTimeKind.Utc), id);
            }
            catch (FormatException)
            {
                throw InvalidCursor();
            }
        }

        private static AppException InvalidCursor()
        {
            return new AppException(
                400,
                ApiErrorCodes.ValidationError,
                "Cursor is invalid.",
                "cursor");
        }
    }
}
