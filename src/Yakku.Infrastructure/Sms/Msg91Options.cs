using Yakku.Application.Auth;

namespace Yakku.Infrastructure.Sms
{
    /// <summary>
    /// MSG91 settings, read from environment variables (Render's dashboard in production, a git-ignored .env locally).
    /// There are no defaults for credentials. The AuthKey never appears in <see cref="ToString"/>, logs or errors.
    /// </summary>
    public sealed class Msg91Options
    {
        public const string EnabledVariable = "MSG91_ENABLED";
        public const string AuthKeyVariable = "MSG91_AUTH_KEY";
        public const string FlowIdVariable = "MSG91_FLOW_ID";
        public const string SenderIdVariable = "MSG91_SENDER_ID";
        public const string OtpExpiryMinutesVariable = "MSG91_OTP_EXPIRY_MINUTES";

        public const int DefaultOtpExpiryMinutes = 5;

        /// <summary>Names of the dynamic variables in the approved MSG91 flow template (<c>##otp##</c>, <c>##expiry##</c>).</summary>
        public const string OtpVariableName = "otp";
        public const string ExpiryVariableName = "expiry";

        private readonly List<string> _errors = [];

        private Msg91Options()
        {
        }

        public bool Enabled { get; private init; } = true;

        /// <summary>The secret. Server-side only.</summary>
        public string AuthKey { get; private init; } = string.Empty;

        /// <summary>The MSG91 flow (template) id.</summary>
        public string FlowId { get; private init; } = string.Empty;

        /// <summary>
        /// The DLT-registered sender header. MSG91 binds it to the flow, so it is validated and kept here but is
        /// not part of the Flow API request.
        /// </summary>
        public string SenderId { get; private init; } = string.Empty;

        public int OtpExpiryMinutes { get; private init; } = DefaultOtpExpiryMinutes;

        public IReadOnlyList<string> Errors => _errors;

        public OtpPolicy OtpPolicy => OtpPolicy.ForExpiryMinutes(OtpExpiryMinutes);

        public static Msg91Options FromEnvironment(Func<string, string?> read)
        {
            var errors = new List<string>();

            var enabled = true;
            var enabledRaw = Clean(read(EnabledVariable));
            if (enabledRaw is not null)
            {
                switch (enabledRaw.ToLowerInvariant())
                {
                    case "true" or "1":
                        enabled = true;
                        break;
                    case "false" or "0":
                        enabled = false;
                        break;
                    default:
                        errors.Add($"{EnabledVariable} must be true or false.");
                        break;
                }
            }

            var expiry = DefaultOtpExpiryMinutes;
            var expiryRaw = Clean(read(OtpExpiryMinutesVariable));
            if (expiryRaw is not null
                && (!int.TryParse(expiryRaw, out expiry) || expiry < 1 || expiry > OtpPolicy.MaxExpiryMinutes))
            {
                errors.Add($"{OtpExpiryMinutesVariable} must be a whole number of minutes between 1 and {OtpPolicy.MaxExpiryMinutes}.");
                expiry = DefaultOtpExpiryMinutes;
            }

            var authKey = Clean(read(AuthKeyVariable)) ?? string.Empty;
            var flowId = Clean(read(FlowIdVariable)) ?? string.Empty;
            var senderId = Clean(read(SenderIdVariable)) ?? string.Empty;

            if (enabled)
            {
                var missing = new List<string>();
                if (authKey.Length == 0)
                {
                    missing.Add(AuthKeyVariable);
                }

                if (flowId.Length == 0)
                {
                    missing.Add(FlowIdVariable);
                }

                if (missing.Count > 0)
                {
                    errors.Add($"MSG91 is enabled but required configuration is missing: {string.Join(", ", missing)}.");
                }

                if (senderId.Length > 0 && !IsValidSenderId(senderId))
                {
                    errors.Add($"{SenderIdVariable} must be 3 to 11 letters or digits.");
                }
            }

            var options = new Msg91Options
            {
                Enabled = enabled,
                AuthKey = authKey,
                FlowId = flowId,
                SenderId = senderId,
                OtpExpiryMinutes = expiry
            };
            options._errors.AddRange(errors);
            return options;
        }

        /// <summary>Stops the application from starting on bad configuration. Messages name variables, never values.</summary>
        public void ThrowIfInvalid()
        {
            if (_errors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Invalid MSG91 configuration. " + string.Join(" ", _errors)
                + " Set the variables in the environment (Render dashboard) or in your local .env file,"
                + $" or set {EnabledVariable}=false to turn SMS delivery off.");
        }

        public override string ToString()
        {
            return $"Msg91Options {{ Enabled = {Enabled}, FlowConfigured = {FlowId.Length > 0}, AuthKeyConfigured = {AuthKey.Length > 0}, OtpExpiryMinutes = {OtpExpiryMinutes} }}";
        }

        private static bool IsValidSenderId(string value)
        {
            return value.Length is >= 3 and <= 11 && value.All(char.IsAsciiLetterOrDigit);
        }

        private static string? Clean(string? value)
        {
            var trimmed = value?.Trim().Trim('"').Trim('\'').Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
