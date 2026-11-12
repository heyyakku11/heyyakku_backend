namespace Yakku.API.AdminSecurity
{
    /// <summary>Writes and clears the admin session cookie. The token only ever leaves the server through this cookie.</summary>
    public sealed class AdminCookieService
    {
        private readonly AdminSecuritySettings _settings;
        private readonly TimeProvider _time;

        public AdminCookieService(AdminSecuritySettings settings, TimeProvider time)
        {
            _settings = settings;
            _time = time;
        }

        public string Name => _settings.CookieName;

        public string? Read(HttpRequest request) =>
            request.Cookies.TryGetValue(_settings.CookieName, out var value) ? value : null;

        public void Write(HttpResponse response, string token, DateTime absoluteExpiresAt)
        {
            var now = _time.GetUtcNow();
            var remaining = absoluteExpiresAt - now.UtcDateTime;
            if (remaining <= TimeSpan.Zero)
            {
                Clear(response);
                return;
            }

            var options = BaseOptions();
            options.MaxAge = remaining;
            options.Expires = now.Add(remaining);
            response.Cookies.Append(_settings.CookieName, token, options);
        }

        /// <summary>Deletes the cookie with the same Path/Secure/SameSite it was set with, so every browser removes it.</summary>
        public void Clear(HttpResponse response)
        {
            response.Cookies.Delete(_settings.CookieName, BaseOptions());
        }

        private CookieOptions BaseOptions() => new()
        {
            HttpOnly = true,
            Secure = _settings.CookieSecure,
            SameSite = _settings.CookieSameSite,
            Path = "/",
            // No Domain attribute: the cookie is host-only, which is also what the __Host- prefix requires.
            IsEssential = true
        };
    }
}
