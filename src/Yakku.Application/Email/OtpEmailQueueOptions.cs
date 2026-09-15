namespace Yakku.Application.Email
{
    public static class OtpEmailQueueOptions
    {
        public const int Capacity = 100;
        public const int MaxAttempts = 3;
        public const string ProviderName = "Resend";
        public static readonly TimeSpan[] RetryDelays =
        [
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(2)
        ];
    }
}
