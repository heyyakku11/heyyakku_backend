namespace Yakku.Application.AdminAuth.Security
{
    /// <summary>Lifetimes and limits of admin cookie sessions. Production values come from ADMIN_* environment variables.</summary>
    public sealed class AdminSessionOptions
    {
        /// <summary>A session unused for this long ends.</summary>
        public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromMinutes(30);

        /// <summary>A session ends this long after login however active it is, so a stolen cookie cannot live forever.</summary>
        public TimeSpan AbsoluteTimeout { get; init; } = TimeSpan.FromHours(12);

        /// <summary>The session token is replaced by a new one once it is this old.</summary>
        public TimeSpan RotationInterval { get; init; } = TimeSpan.FromMinutes(15);

        /// <summary>After a rotation the previous token still works this long, for requests already in flight.</summary>
        public TimeSpan RotationGrace { get; init; } = TimeSpan.FromSeconds(30);

        /// <summary>Last-activity is written at most this often, so reads do not become writes.</summary>
        public TimeSpan ActivityWriteInterval { get; init; } = TimeSpan.FromMinutes(1);

        /// <summary>Failed logins for one email from one address before that pair is blocked.</summary>
        public int LoginPairFailureLimit { get; init; } = 5;

        /// <summary>Failed logins for one email from any address before the account is slowed down (kept high so it cannot easily lock the real admin out).</summary>
        public int LoginAccountFailureLimit { get; init; } = 20;

        /// <summary>Failed logins from one address, for any email, before it is blocked (credential stuffing).</summary>
        public int LoginIpFailureLimit { get; init; } = 30;

        /// <summary>Failures are forgotten after this long without a new one.</summary>
        public TimeSpan LoginFailureWindow { get; init; } = TimeSpan.FromMinutes(15);

        /// <summary>First block length; it doubles with each further failure up to <see cref="LoginMaxBlock"/>.</summary>
        public TimeSpan LoginBaseBlock { get; init; } = TimeSpan.FromSeconds(15);

        /// <summary>Blocks never exceed this, so an attacker cannot lock an admin out for long.</summary>
        public TimeSpan LoginMaxBlock { get; init; } = TimeSpan.FromMinutes(15);

        /// <summary>Key for CSRF tokens (derived from JWT_SECRET, never the secret itself).</summary>
        public byte[] CsrfKey { get; init; } = [];
    }
}
