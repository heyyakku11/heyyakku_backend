using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Yakku.Application.Configuration;
using Yakku.Application.Email;

namespace Yakku.Infrastructure.Email
{
    /// <summary>Maps an admin email to its Resend template id from bound <see cref="ResendOptions"/>.</summary>
    public static class AdminEmailTemplateCatalog
    {
        public static string? TemplateId(AdminEmailTemplate template, IServiceProvider services)
        {
            var options = services.GetRequiredService<IOptions<ResendOptions>>().Value;
            return TemplateId(template, options);
        }

        public static string? TemplateId(AdminEmailTemplate template, ResendOptions options)
        {
            var value = template switch
            {
                AdminEmailTemplate.Registration => options.AdminTemplates.Register,
                AdminEmailTemplate.ForgotPassword => options.AdminTemplates.ForgotPassword,
                _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
            };

            value = value?.Trim().Trim('"');
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }

        public static string ConfigKey(AdminEmailTemplate template)
        {
            return template switch
            {
                AdminEmailTemplate.Registration => "Resend:AdminTemplates:Register",
                AdminEmailTemplate.ForgotPassword => "Resend:AdminTemplates:ForgotPassword",
                _ => throw new ArgumentOutOfRangeException(nameof(template), template, null)
            };
        }
    }
}
