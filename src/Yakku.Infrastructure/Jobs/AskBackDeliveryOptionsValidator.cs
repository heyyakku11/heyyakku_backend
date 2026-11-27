using Microsoft.Extensions.Options;
using Yakku.Application.AskBack;

namespace Yakku.Infrastructure.Jobs
{
    /// <summary>Stops the application at startup, naming every bad setting, instead of misbehaving at run time.</summary>
    public sealed class AskBackDeliveryOptionsValidator : IValidateOptions<AskBackDeliveryOptions>
    {
        public ValidateOptionsResult Validate(string? name, AskBackDeliveryOptions options)
        {
            var errors = options.Validate();
            return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
        }
    }
}
