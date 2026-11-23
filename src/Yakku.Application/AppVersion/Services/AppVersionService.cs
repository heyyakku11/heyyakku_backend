using FluentValidation;
using Microsoft.Extensions.Options;
using Yakku.Application.AppVersion.DTOs;
using Yakku.Application.AppVersion.Interfaces;
using Yakku.Application.Configuration;
using Yakku.Application.Devices;
using Yakku.Domain.Enums;

namespace Yakku.Application.AppVersion.Services
{
    public class AppVersionService : IAppVersionService
    {
        private readonly AppVersionOptions _options;
        private readonly IValidator<AppVersionCheckRequest> _validator;

        public AppVersionService(
            IOptions<AppVersionOptions> options,
            IValidator<AppVersionCheckRequest> validator)
        {
            _options = options.Value;
            _validator = validator;
        }

        public async Task<AppVersionCheckResponse> CheckAsync(
            AppVersionCheckRequest request,
            CancellationToken cancellationToken = default)
        {
            await _validator.ValidateAndThrowAsync(request, cancellationToken);

            DeviceFieldParser.TryParsePlatform(request.Platform, out var platform);
            SemVersion.TryParse(request.Version, out var current);

            var platformOptions = platform == DevicePlatform.iOS
                ? _options.Ios
                : _options.Android;

            var minSupportedRaw = string.IsNullOrWhiteSpace(platformOptions.MinSupported)
                ? "1.0.0"
                : platformOptions.MinSupported.Trim();
            var latestRaw = string.IsNullOrWhiteSpace(platformOptions.Latest)
                ? minSupportedRaw
                : platformOptions.Latest.Trim();

            if (!SemVersion.TryParse(minSupportedRaw, out var minSupported))
            {
                minSupported = new SemVersion.Value(1, 0, 0);
                minSupportedRaw = "1.0.0";
            }

            if (!SemVersion.TryParse(latestRaw, out var latest))
            {
                latest = minSupported;
                latestRaw = minSupportedRaw;
            }

            // A misconfigured Latest below MinSupported must not hide a force update.
            if (latest.CompareTo(minSupported) < 0)
            {
                latest = minSupported;
                latestRaw = minSupportedRaw;
            }

            var forceUpdate = current.CompareTo(minSupported) < 0;
            var updateRequired = forceUpdate || current.CompareTo(latest) < 0;
            var message = updateRequired
                ? (string.IsNullOrWhiteSpace(_options.Message)
                    ? "Please update to continue using Yakku."
                    : _options.Message.Trim())
                : string.Empty;

            return new AppVersionCheckResponse
            {
                Platform = platform == DevicePlatform.iOS ? "ios" : "android",
                CurrentVersion = current.ToString(),
                MinSupportedVersion = minSupportedRaw,
                LatestVersion = latestRaw,
                UpdateRequired = updateRequired,
                ForceUpdate = forceUpdate,
                StoreUrl = platformOptions.StoreUrl?.Trim() ?? string.Empty,
                Message = message
            };
        }
    }
}
