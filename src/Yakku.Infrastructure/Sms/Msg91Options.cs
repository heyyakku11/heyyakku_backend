using Microsoft.Extensions.Configuration;

namespace Yakku.Infrastructure.Sms
{
    /// <summary>
    /// MSG91 settings bound from the <c>Msg91</c> configuration section (appsettings + flat env bridge).
    /// There are no defaults for credentials. The AuthKey never appears in <see cref="ToString"/>, logs or errors.
    /// </summary>
    public sealed class Msg91Options
    {
        public const string SectionName = "Msg91";

        public const string EnabledVariable = "MSG91_ENABLED";
        public const string AuthKeyVariable = "MSG91_AUTH_KEY";
        public const string FlowIdVariable = "MSG91_FLOW_ID";
        public const string SenderIdVariable = "MSG91_SENDER_ID";

        /// <summary>Name of the one dynamic variable in the approved MSG91 flow template (<c>##otp##</c>).</summary>
        public const string OtpVariableName = "otp";

        private readonly List<string> _errors = [];

        private Msg91Options()
        {
        }

        public bool Enabled { get; private init; }

        /// <summary>The secret. Server-side only.</summary>
        public string AuthKey { get; private init; } = string.Empty;

        /// <summary>The MSG91 flow (template) id.</summary>
        public string FlowId { get; private init; } = string.Empty;

        /// <summary>
        /// The DLT-registered sender header. MSG91 binds it to the flow, so it is validated and kept here but is
        /// not part of the Flow API request.
        /// </summary>
        public string SenderId { get; private init; } = string.Empty;

        public IReadOnlyList<string> Errors => _errors;

        /// <summary>Reads nested <c>Msg91:*</c> keys from configuration (after the flat-env bridge).</summary>
        public static Msg91Options FromConfiguration(IConfiguration configuration)
        {
            var section = configuration.GetSection(SectionName);
            return FromReaders(
                () => Clean(section["Enabled"]),
                () => Clean(section["AuthKey"]),
                () => Clean(section["FlowId"]),
                () => Clean(section["SenderId"]),
                enabledLabel: $"{SectionName}:Enabled",
                authKeyLabel: $"{SectionName}:AuthKey",
                flowIdLabel: $"{SectionName}:FlowId",
                senderIdLabel: $"{SectionName}:SenderId",
                defaultEnabled: false);
        }

        /// <summary>Reads flat MSG91_* environment-style keys (used by unit tests and legacy callers).</summary>
        public static Msg91Options FromEnvironment(Func<string, string?> read)
        {
            return FromReaders(
                () => Clean(read(EnabledVariable)),
                () => Clean(read(AuthKeyVariable)),
                () => Clean(read(FlowIdVariable)),
                () => Clean(read(SenderIdVariable)),
                enabledLabel: EnabledVariable,
                authKeyLabel: AuthKeyVariable,
                flowIdLabel: FlowIdVariable,
                senderIdLabel: SenderIdVariable,
                defaultEnabled: true);
        }

        private static Msg91Options FromReaders(
            Func<string?> readEnabled,
            Func<string?> readAuthKey,
            Func<string?> readFlowId,
            Func<string?> readSenderId,
            string enabledLabel,
            string authKeyLabel,
            string flowIdLabel,
            string senderIdLabel,
            bool defaultEnabled)
        {
            var errors = new List<string>();

            var enabled = defaultEnabled;
            var enabledRaw = readEnabled();
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
                        errors.Add($"{enabledLabel} must be true or false.");
                        break;
                }
            }

            var authKey = readAuthKey() ?? string.Empty;
            var flowId = readFlowId() ?? string.Empty;
            var senderId = readSenderId() ?? string.Empty;

            if (enabled)
            {
                var missing = new List<string>();
                if (authKey.Length == 0)
                {
                    missing.Add(authKeyLabel);
                }

                if (flowId.Length == 0)
                {
                    missing.Add(flowIdLabel);
                }

                if (missing.Count > 0)
                {
                    errors.Add($"MSG91 is enabled but required configuration is missing: {string.Join(", ", missing)}.");
                }

                if (senderId.Length > 0 && !IsValidSenderId(senderId))
                {
                    errors.Add($"{senderIdLabel} must be 3 to 11 letters or digits.");
                }
            }

            var options = new Msg91Options
            {
                Enabled = enabled,
                AuthKey = authKey,
                FlowId = flowId,
                SenderId = senderId
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
                + " Set credentials in the environment (Render dashboard) or in your local .env file,"
                + $" or set {SectionName}:Enabled=false in appsettings to turn SMS delivery off.");
        }

        public override string ToString()
        {
            return $"Msg91Options {{ Enabled = {Enabled}, FlowConfigured = {FlowId.Length > 0}, AuthKeyConfigured = {AuthKey.Length > 0} }}";
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
