using FluentValidation;
using Yakku.Application.Payments.DTOs;

namespace Yakku.Application.Payments.Validators
{
    public class VerifyPaymentValidator : AbstractValidator<VerifyPaymentRequest>
    {
        public VerifyPaymentValidator()
        {
            RuleFor(x => x.RazorpayOrderId).NotEmpty().MaximumLength(64);
            RuleFor(x => x.RazorpayPaymentId).NotEmpty().MaximumLength(64);
            RuleFor(x => x.RazorpaySignature).NotEmpty().MaximumLength(128);
        }
    }
}
