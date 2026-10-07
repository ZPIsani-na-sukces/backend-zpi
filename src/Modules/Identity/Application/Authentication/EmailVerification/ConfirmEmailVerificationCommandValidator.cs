using FluentValidation;

namespace Zpi.Modules.Identity.Application.Authentication.EmailVerification;

public sealed class ConfirmEmailVerificationCommandValidator : AbstractValidator<ConfirmEmailVerificationCommand>
{
    public ConfirmEmailVerificationCommandValidator()
    {
        RuleFor(command => command.ChallengeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(256);
    }
}
