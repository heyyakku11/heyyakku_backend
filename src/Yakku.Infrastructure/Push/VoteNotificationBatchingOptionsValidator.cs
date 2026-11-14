using Microsoft.Extensions.Options;
using Yakku.Application.PushNotifications.Batching;

namespace Yakku.Infrastructure.Push
{
    /// <summary>Stops the application at startup, naming every bad setting, instead of misbehaving at run time.</summary>
    public sealed class VoteNotificationBatchingOptionsValidator : IValidateOptions<VoteNotificationBatchingOptions>
    {
        public ValidateOptionsResult Validate(string? name, VoteNotificationBatchingOptions options)
        {
            var errors = options.Validate();
            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }
    }
}
