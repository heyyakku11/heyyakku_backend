namespace Yakku.Application.AdminAuth
{
    public static class AdminEmailPolicy
    {
        public const string AllowedDomain = "heyyakku.com";

        public static bool IsAllowed(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return false;
            }

            var normalized = email.Trim().ToLowerInvariant();
            var at = normalized.IndexOf('@');
            if (at <= 0 || at != normalized.LastIndexOf('@'))
            {
                return false;
            }

            return normalized[(at + 1)..] == AllowedDomain;
        }
    }
}
