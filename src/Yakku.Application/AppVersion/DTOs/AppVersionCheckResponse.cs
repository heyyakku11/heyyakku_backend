namespace Yakku.Application.AppVersion.DTOs
{
    public class AppVersionCheckResponse
    {
        public string Platform { get; set; } = string.Empty;

        public string CurrentVersion { get; set; } = string.Empty;

        public string MinSupportedVersion { get; set; } = string.Empty;

        public string LatestVersion { get; set; } = string.Empty;

        public bool UpdateRequired { get; set; }

        public bool ForceUpdate { get; set; }

        public string StoreUrl { get; set; } = string.Empty;

        public string Message { get; set; } = string.Empty;
    }
}
