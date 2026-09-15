namespace Yakku.Application.Auth.Models
{
    public sealed class EmailSendResult
    {
        private EmailSendResult(
            bool succeeded,
            bool isTransientFailure,
            string? providerMessageId,
            string? errorCode,
            string? errorMessage)
        {
            Succeeded = succeeded;
            IsTransientFailure = isTransientFailure;
            ProviderMessageId = providerMessageId;
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
        }

        public bool Succeeded { get; }
        public bool IsTransientFailure { get; }
        public bool IsPermanentFailure => !Succeeded && !IsTransientFailure;
        public string? ProviderMessageId { get; }
        public string? ErrorCode { get; }
        public string? ErrorMessage { get; }

        public static EmailSendResult Success(string? providerMessageId)
        {
            return new EmailSendResult(true, false, providerMessageId, null, null);
        }

        public static EmailSendResult TransientFailure(string errorCode, string? errorMessage)
        {
            return new EmailSendResult(false, true, null, errorCode, errorMessage);
        }

        public static EmailSendResult PermanentFailure(string errorCode, string? errorMessage)
        {
            return new EmailSendResult(false, false, null, errorCode, errorMessage);
        }
    }
}
