using FluentValidation;

namespace Zpi.Modules.Identity.Application.Authentication.PhoneVerification;

public sealed class ConfirmPhoneVerificationCommandValidator : AbstractValidator<ConfirmPhoneVerificationCommand>
{
    public ConfirmPhoneVerificationCommandValidator()
    {
        RuleFor(command => command.ChallengeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().Matches(@"\A[0-9]{6}\z");
    }
}
