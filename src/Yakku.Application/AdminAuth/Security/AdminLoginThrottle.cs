using Yakku.Application.AdminAuth.Interfaces;

namespace Yakku.Application.AdminAuth.Security
{
    /// <summary>
    /// Slows password guessing on three axes so no single one can be abused to lock a real admin out:
    /// the (email, address) pair is strict, the account is lenient, and the address is lenient across emails
    /// (credential stuffing). A block grows with each further failure (progressive delay) but is always capped,
    /// and it expires on its own, so nobody is locked out permanently. Blocks apply the same way to emails that
    /// do not exist, so the response reveals nothing about accounts.
    /// </summary>
    public sealed class AdminLoginThrottle
    {
        private readonly IAdminAuthStore _store;
        private readonly AdminSessionOptions _options;

        public AdminLoginThrottle(IAdminAuthStore store, AdminSessionOptions options)
        {
            _store = store;
            _options = options;
        }

        public async Task<bool> IsBlockedAsync(string email, string? ip, CancellationToken cancellationToken)
        {
            foreach (var (key, _) in Axes(email, ip))
            {
                if (await _store.IsLoginBlockedAsync(key, cancellationToken))
                {
                    return true;
                }
            }

            return false;
        }

        public async Task RecordFailureAsync(string email, string? ip, CancellationToken cancellationToken)
        {
            foreach (var (key, limit) in Axes(email, ip))
            {
                await _store.RecordLoginFailureAsync(key, _options.LoginFailureWindow, cancellationToken);
                var failures = await _store.GetLoginFailuresAsync(key, cancellationToken);
                if (failures >= limit)
                {
                    await _store.SetLoginBlockAsync(key, BlockFor(failures - limit), cancellationToken);
                }
            }
        }

        /// <summary>A good login clears the strict pair counter only; account and address counters age out on their own.</summary>
        public Task RecordSuccessAsync(string email, string? ip, CancellationToken cancellationToken)
        {
            return _store.ClearLoginFailuresAsync(PairKey(email, ip), cancellationToken);
        }

        private TimeSpan BlockFor(int extraFailures)
        {
            var factor = Math.Pow(2, Math.Min(extraFailures, 20));
            var seconds = Math.Min(_options.LoginBaseBlock.TotalSeconds * factor, _options.LoginMaxBlock.TotalSeconds);
            return TimeSpan.FromSeconds(seconds);
        }

        private IEnumerable<(string Key, int Limit)> Axes(string email, string? ip)
        {
            yield return (PairKey(email, ip), _options.LoginPairFailureLimit);
            yield return ($"acct:{email}", _options.LoginAccountFailureLimit);
            yield return ($"ip:{ip ?? "unknown"}", _options.LoginIpFailureLimit);
        }

        private static string PairKey(string email, string? ip) => $"pair:{email}|{ip ?? "unknown"}";
    }
}
