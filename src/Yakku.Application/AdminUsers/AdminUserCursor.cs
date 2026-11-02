using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Encoding = System.Text.Encoding;

namespace Yakku.Application.AdminUsers
{
    /// <summary>An opaque position in the (created at, id) ordering of the admin user and guest lists.</summary>
    public static class AdminUserCursor
    {
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
                if (separator <= 0
                    || !long.TryParse(payload[..separator], out var ticks)
                    || ticks < DateTime.MinValue.Ticks
                    || ticks > DateTime.MaxValue.Ticks
                    || !Guid.TryParse(payload[(separator + 1)..], out var id))
                {
                    throw Invalid();
                }

                return (new DateTime(ticks, DateTimeKind.Utc), id);
            }
            catch (FormatException)
            {
                throw Invalid();
            }
        }

        private static AppException Invalid()
        {
            return new AppException(400, ApiErrorCodes.ValidationError, "Cursor is invalid.", "cursor");
        }
    }
}
