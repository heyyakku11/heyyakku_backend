namespace Yakku.Application.Caching
{
    /// <summary>Stable Redis key shapes for cached GET payloads (<c>yakku:resp:v2:</c> prefix; SWR envelope).</summary>
    public static class ResponseCacheKeys
    {
        public const string Prefix = "yakku:resp:v2:";

        public static string PlansAll => Prefix + "plans:all";

        public static string CheckThemesAll => Prefix + "check-themes:all";

        public static string PersonalityTraits => Prefix + "personality:traits";

        public static string FunCatalog => Prefix + "fun:catalog";

        /// <summary>
        /// Shared live-week check catalog (no per-user <c>used</c>). One Redis key for every caller.
        /// </summary>
        public static string ChecksWeekCatalog(DateOnly weekStart) =>
            Prefix + $"checks:week:{weekStart:yyyy-MM-dd}:catalog";

        /// <summary>Legacy anon list key; still invalidated so old entries expire immediately.</summary>
        public static string ChecksWeekAnon(DateOnly weekStart) =>
            Prefix + $"checks:week:{weekStart:yyyy-MM-dd}:anon";

        /// <summary>Legacy per-user list key; still invalidated so old entries expire immediately.</summary>
        public static string ChecksWeekUser(DateOnly weekStart, Guid userId) =>
            Prefix + $"checks:week:{weekStart:yyyy-MM-dd}:u:{userId:N}";

        /// <summary>Keys to drop when the live-week schedule changes (admin create/update/delete).</summary>
        public static IReadOnlyList<string> AfterWeekScheduleChange(DateOnly weekStart) =>
        [
            ChecksWeekCatalog(weekStart),
            ChecksWeekAnon(weekStart)
        ];

        public static string FunList(Guid userId) => Prefix + $"fun:list:u:{userId:N}";

        public static string FunListAnon => Prefix + "fun:list:anon";

        public static string FunTypes(Guid userId) => Prefix + $"fun:types:u:{userId:N}";

        public static string FunTypesAnon => Prefix + "fun:types:anon";

        public static string FunType(Guid typeId, Guid userId) =>
            Prefix + $"fun:type:{typeId:N}:u:{userId:N}";

        public static string FunTypeAnon(Guid typeId) => Prefix + $"fun:type:{typeId:N}:anon";

        public static string ShareViewer(string shareCode, string viewerKey) =>
            Prefix + $"share:{shareCode}:v:{viewerKey}";

        public static string ShareResult(string shareCode) => Prefix + $"share:{shareCode}:result";

        public static string PollOptionReasons(Guid pollId, Guid optionId, Guid ownerId) =>
            Prefix + $"reasons:poll:{pollId:N}:opt:{optionId:N}:u:{ownerId:N}";

        public static string CheckOptionReasons(Guid userCheckId, Guid optionId, Guid ownerId) =>
            Prefix + $"reasons:check:{userCheckId:N}:opt:{optionId:N}:u:{ownerId:N}";

        public static string PollCustomAnswers(Guid pollId, Guid ownerId) =>
            Prefix + $"reasons:poll:{pollId:N}:custom:u:{ownerId:N}";

        public static string CheckCustomAnswers(Guid userCheckId, Guid ownerId) =>
            Prefix + $"reasons:check:{userCheckId:N}:custom:u:{ownerId:N}";

        /// <summary>
        /// Keys to drop after the user makes a weekly check or fun card.
        /// Week catalog stays: <c>used</c> is applied from the DB on each list read.
        /// Legacy per-user week keys are still dropped so stale copies cannot linger.
        /// </summary>
        public static IReadOnlyList<string> AfterUserMadeCard(
            Guid userId,
            DateOnly weekStart,
            Guid? funTypeId = null)
        {
            var keys = new List<string>
            {
                ChecksWeekUser(weekStart, userId),
                FunList(userId),
                FunTypes(userId)
            };
            if (funTypeId is { } typeId)
            {
                keys.Add(FunType(typeId, userId));
            }

            return keys;
        }

        /// <summary>Keys to drop when fun-card saved state changes for a user.</summary>
        public static IReadOnlyList<string> AfterFunSaveChange(Guid userId, Guid? funTypeId = null)
        {
            var keys = new List<string>
            {
                FunList(userId),
                FunTypes(userId)
            };
            if (funTypeId is { } typeId)
            {
                keys.Add(FunType(typeId, userId));
            }

            return keys;
        }

        /// <summary>
        /// Share/result keys to drop after vote/close/reshare. Drops anon + known viewer/owner viewer keys;
        /// other viewers refresh within the short share TTL.
        /// </summary>
        public static IReadOnlyList<string> AfterShareMutation(
            string shareCode,
            Guid? ownerId = null,
            Guid? viewerId = null)
        {
            var keys = new List<string>
            {
                ShareResult(shareCode),
                ShareViewer(shareCode, "anon")
            };
            if (ownerId is { } owner)
            {
                keys.Add(ShareViewer(shareCode, owner.ToString("N")));
            }

            if (viewerId is { } viewer && viewer != ownerId)
            {
                keys.Add(ShareViewer(shareCode, viewer.ToString("N")));
            }

            return keys;
        }

        public static string ViewerKey(Guid? viewerId) =>
            viewerId is { } id ? id.ToString("N") : "anon";

        public static string UsersMe(Guid userId) => Prefix + $"users:me:u:{userId:N}";

        public static string PersonalityCardMe(Guid userId) => Prefix + $"personality:card:me:u:{userId:N}";

        public static string AsksAsked(Guid userId, string? cursor) =>
            Prefix + $"asks:asked:u:{userId:N}:c:{CursorSegment(cursor)}";

        public static string ChecksMine(Guid userId, string? cursor) =>
            Prefix + $"checks:mine:u:{userId:N}:c:{CursorSegment(cursor)}";

        public static string NotifUnread(Guid userId) => Prefix + $"notif:unread:u:{userId:N}";

        public static string AskBackList(Guid userId, string? cursor) =>
            Prefix + $"askback:list:u:{userId:N}:c:{CursorSegment(cursor)}";

        public static string ChecksUsed(Guid userId) => Prefix + $"checks:used:u:{userId:N}";

        /// <summary>Keys to drop when a user's profile stats/plan may have changed.</summary>
        public static IReadOnlyList<string> AfterUserProfileChange(Guid userId) =>
        [
            UsersMe(userId)
        ];

        /// <summary>Keys to drop after vote / make / close that affect list pages (cursor-agnostic prefix drop via known first page).</summary>
        public static IReadOnlyList<string> AfterAskListMutation(Guid userId) =>
        [
            AsksAsked(userId, null),
            ChecksMine(userId, null),
            UsersMe(userId),
            ChecksUsed(userId)
        ];

        public static IReadOnlyList<string> AfterNotificationChange(Guid userId) =>
        [
            NotifUnread(userId)
        ];

        public static IReadOnlyList<string> AfterAskBackListChange(Guid userId) =>
        [
            AskBackList(userId, null)
        ];

        private static string CursorSegment(string? cursor)
        {
            if (string.IsNullOrWhiteSpace(cursor))
            {
                return "_";
            }

            var hash = global::System.Security.Cryptography.SHA256.HashData(
                global::System.Text.Encoding.UTF8.GetBytes(cursor));
            return Convert.ToHexString(hash)[..16];
        }
    }
}
