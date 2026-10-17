namespace Yakku.Application.Email
{
    public enum AdminEmailTemplate
    {
        Registration,
        ForgotPassword
    }

    /// <summary>Template variable names shared by all admin emails. Rename here if the Resend templates differ.</summary>
    public static class AdminEmailVariables
    {
        public const string Heading = "heading";
        public const string Email = "email";
        public const string WebUrl = "web_url";
        public const string ExpiryTime = "expiry_time";
    }

    public sealed record AdminEmailJob(
        Guid CorrelationId,
        AdminEmailTemplate Template,
        string RecipientEmail,
        IReadOnlyDictionary<string, string> Variables);
}
