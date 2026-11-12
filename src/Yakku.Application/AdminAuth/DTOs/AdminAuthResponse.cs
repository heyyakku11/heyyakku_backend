namespace Yakku.Application.AdminAuth.DTOs
{
    /// <summary>
    /// Body of a successful admin login. The session itself travels only in the HttpOnly cookie; this carries
    /// just the CSRF token the browser sends back in the X-CSRF-Token header on state-changing requests.
    /// </summary>
    public class AdminAuthResponse
    {
        public string CsrfToken { get; set; } = string.Empty;
    }

    /// <summary>The signed-in admin as the server sees them. The frontend restores its state from this, never from storage.</summary>
    public class AdminSessionResponse
    {
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string CsrfToken { get; set; } = string.Empty;
    }
}
