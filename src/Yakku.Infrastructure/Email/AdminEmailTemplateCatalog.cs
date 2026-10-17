using Yakku.Application.Email;

namespace Yakku.Infrastructure.Email
{
    /// <summary>Maps an admin email to its Resend template id (from the environment, read on use).</summary>
    public static class AdminEmailTemplateCatalog
    {
        public static string? TemplateId(AdminEmailTemplate template)
        {
            var value = Environment.GetEnvironmentVariable(EnvName(template))?.Trim().Trim('"');
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public static string EnvName(AdminEmailTemplate template)
        {
            return template switch
            {
                AdminEmailTemplate.Registration => "RESEND_ADMIN_REGISTER_TEMPLATE_ID",
                AdminEmailTemplate.ForgotPassword => "RESEND_ADMIN_FORGOT_PASSWORD_TEMPLATE_ID",
                AdminEmailTemplate.ChangePassword => "RESEND_ADMIN_CHANGE_PASSWORD_TEMPLATE_ID",
                _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
            };
        }
    }
}
