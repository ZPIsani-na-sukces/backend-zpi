using FluentValidation;

namespace Zpi.Modules.Identity.Application.Authentication.PhoneVerification;

public sealed class StartPhoneVerificationCommandValidator : AbstractValidator<StartPhoneVerificationCommand>
{
    public StartPhoneVerificationCommandValidator()
    {
        RuleFor(command => command.VerificationTicket).NotEmpty().MaximumLength(256);
    }
}
