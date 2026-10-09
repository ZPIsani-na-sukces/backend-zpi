using FluentValidation;

namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed class VerifyPasswordResetCodeCommandValidator : AbstractValidator<VerifyPasswordResetCodeCommand>
{
    public VerifyPasswordResetCodeCommandValidator()
    {
        RuleFor(command => command.ChallengeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(256);
    }
}
