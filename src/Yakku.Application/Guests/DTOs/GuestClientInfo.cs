using Yakku.Domain.Enums;

namespace Yakku.Application.Guests.DTOs
{
    /// <summary>
    /// What the API layer learned about the caller from request headers when a guest is created. Every member is
    /// optional; nothing here is required for a guest to be issued.
    /// </summary>
    public sealed record GuestClientInfo(
        string? IpAddress = null,
        string? UserAgent = null,
        string? BrowserName = null,
        string? BrowserVersion = null,
        string? OsName = null,
        GuestDeviceType? DeviceType = null,
        string? Language = null,
        string? CountryCode = null,
        bool? CookiesEnabled = null);
}
