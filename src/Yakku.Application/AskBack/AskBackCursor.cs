using Yakku.Application.Common.Exceptions;
using Yakku.Application.Common.Responses;
using Encoding = System.Text.Encoding;

namespace Yakku.Application.AskBack
{
    /// <summary>An opaque position in a (timestamp, id) ordering, used by the conversation and message pages.</summary>
    public static class AskBackCursor
    {
        public static string Encode(DateTime timestamp, Guid id)
        {
            var payload = $"{timestamp.Ticks}:{id:D}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(payload));
        }

        public static (DateTime Timestamp, Guid Id)? TryDecode(string? cursor)
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
                    || !Guid.TryParse(payload[(separator + 1)..], out var id)
                    || id == Guid.Empty)
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
