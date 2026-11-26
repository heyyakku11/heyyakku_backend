namespace Yakku.Application.Auth
{
    /// <summary>
    /// A single, config-driven test account (for app-store reviewers): one phone number that signs in with a fixed
    /// 6-digit code and is never sent an SMS. Bound from <c>Otp:ReviewAccount:*</c> (environment variables
    /// <c>Otp__ReviewAccount__Enabled</c>, <c>__PhoneNumber</c>, <c>__Code</c>). Off unless <c>Enabled</c> is true.
    /// The code is a secret: it never appears in <see cref="ToString"/>, logs or validation messages.
    /// </summary>
    public sealed class ReviewAccountOptions
    {
        public const string SectionName = "Otp:ReviewAccount";

        public const string EnabledVariable = "Otp__ReviewAccount__Enabled";
        public const string PhoneNumberVariable = "Otp__ReviewAccount__PhoneNumber";
        public const string CodeVariable = "Otp__ReviewAccount__Code";

        private readonly List<string> _errors = [];

        private ReviewAccountOptions()
        {
        }

        /// <summary>The default: no review account, every number follows the normal OTP flow.</summary>
        public static ReviewAccountOptions Disabled { get; } = new();

        public bool Enabled { get; private init; }

        /// <summary>The review phone number in normalised E.164 form (+91 and ten digits). Empty when disabled.</summary>
        public string PhoneNumber { get; private init; } = string.Empty;

        /// <summary>The fixed OTP. Server-side only.</summary>
        public string Code { get; private init; } = string.Empty;

        public IReadOnlyList<string> Errors => _errors;

        /// <summary>
        /// Builds the options from raw setting values. Values are only inspected when the account is enabled, so a
        /// half-filled but disabled section never stops the app from starting.
        /// </summary>
        public static ReviewAccountOptions Create(string? enabled, string? phoneNumber, string? code)
        {
            var errors = new List<string>();

            var isEnabled = false;
            var enabledRaw = Clean(enabled);
            if (enabledRaw is not null)
            {
                switch (enabledRaw.ToLowerInvariant())
                {
                    case "true" or "1":
                        isEnabled = true;
                        break;
                    case "false" or "0":
                        break;
                    default:
                        errors.Add($"{EnabledVariable} must be true or false.");
                        break;
                }
            }

            if (!isEnabled)
            {
                var invalid = new ReviewAccountOptions();
                invalid._errors.AddRange(errors);
                return errors.Count == 0 ? Disabled : invalid;
            }

            var normalizedPhone = string.Empty;
            var phoneRaw = Clean(phoneNumber);
            if (phoneRaw is null
                || !PhoneNumberNormalizer.TryNormalize(phoneRaw, out normalizedPhone)
                || !IndianMobileNumber.IsValid(normalizedPhone))
            {
                errors.Add($"{PhoneNumberVariable} must be an Indian mobile number in the form +91 followed by ten digits.");
                normalizedPhone = string.Empty;
            }

            var codeRaw = Clean(code) ?? string.Empty;
            if (codeRaw.Length != OtpOptions.Length || codeRaw.Any(character => character is < '0' or > '9'))
            {
                errors.Add($"{CodeVariable} must be exactly {OtpOptions.Length} digits.");
                codeRaw = string.Empty;
            }

            var options = new ReviewAccountOptions
            {
                Enabled = true,
                PhoneNumber = normalizedPhone,
                Code = codeRaw
            };
            options._errors.AddRange(errors);
            return options;
        }

        /// <summary>True when the review account is on and this normalised number is the review number.</summary>
        public bool IsReviewAccount(string normalizedPhoneNumber)
        {
            return Enabled
                && _errors.Count == 0
                && string.Equals(PhoneNumber, normalizedPhoneNumber, StringComparison.Ordinal);
        }

        /// <summary>Stops the application from starting on bad configuration. Messages name variables, never values.</summary>
        public void ThrowIfInvalid()
        {
            if (_errors.Count == 0)
            {
                return;
            }

            throw new InvalidOperationException(
                "Invalid review account configuration. " + string.Join(" ", _errors)
                + $" Fix the values, or set {EnabledVariable}=false to turn the review account off.");
        }

        public override string ToString()
        {
            return $"ReviewAccountOptions {{ Enabled = {Enabled}, PhoneConfigured = {PhoneNumber.Length > 0}, CodeConfigured = {Code.Length > 0} }}";
        }

        private static string? Clean(string? value)
        {
            var trimmed = value?.Trim().Trim('"').Trim('\'').Trim();
            return string.IsNullOrEmpty(trimmed) ? null : trimmed;
        }
    }
}
