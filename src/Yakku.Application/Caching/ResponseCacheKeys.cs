namespace Yakku.Application.Caching
{
    /// <summary>Stable Redis key shapes for cached GET payloads (<c>yakku:resp:v1:</c> prefix).</summary>
    public static class ResponseCacheKeys
    {
        public const string Prefix = "yakku:resp:v1:";

        public static string PlansAll => Prefix + "plans:all";

        public static string CheckThemesAll => Prefix + "check-themes:all";

        public static string PersonalityTraits => Prefix + "personality:traits";

        public static string FunCatalog => Prefix + "fun:catalog";

        public static string ChecksWeekAnon(DateOnly weekStart) =>
            Prefix + $"checks:week:{weekStart:yyyy-MM-dd}:anon";

        public static string ChecksWeekUser(DateOnly weekStart, Guid userId) =>
            Prefix + $"checks:week:{weekStart:yyyy-MM-dd}:u:{userId:N}";

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

        /// <summary>Keys to drop after the user makes a weekly check or fun card.</summary>
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
    }
}
