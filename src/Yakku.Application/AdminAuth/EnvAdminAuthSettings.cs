using Yakku.Application.AdminAuth.Interfaces;

namespace Yakku.Application.AdminAuth
{
    /// <summary>Reads the admin auth settings from the environment on use, so the app can start without them.</summary>
    public sealed class EnvAdminAuthSettings : IAdminAuthSettings
    {
        public string SecretCode => Read("ADMIN_SECRET_CODE");

        public string WebBaseUrl => Read("ADMIN_WEB_BASE_URL").TrimEnd('/');

        private static string Read(string name)
        {
            var value = Environment.GetEnvironmentVariable(name)?.Trim().Trim('"');
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException($"{name} is not set. Add it to your .env file or environment variables.");
            }

            return value;
        }
    }
}
