using Microsoft.Extensions.Options;
using Yakku.Application.AdminAuth.Interfaces;
using Yakku.Application.Configuration;

namespace Yakku.Application.AdminAuth
{
    /// <summary>Reads admin auth settings from the bound <see cref="AdminOptions"/> section.</summary>
    public sealed class ConfigurationAdminAuthSettings : IAdminAuthSettings
    {
        private readonly AdminOptions _options;

        public ConfigurationAdminAuthSettings(IOptions<AdminOptions> options)
        {
            _options = options.Value;
        }

        public string SecretCode
        {
            get
            {
                var value = _options.SecretCode?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        "Admin:SecretCode is not set. Add ADMIN_SECRET_CODE to your .env file or environment variables.");
                }

                return value;
            }
        }

        public string WebBaseUrl
        {
            get
            {
                var value = _options.WebBaseUrl?.Trim().Trim('"');
                if (string.IsNullOrWhiteSpace(value))
                {
                    throw new InvalidOperationException(
                        "Admin:WebBaseUrl is not set. Add ADMIN_WEB_BASE_URL to your .env file or environment variables.");
                }

                return value.TrimEnd('/');
            }
        }
    }
}
