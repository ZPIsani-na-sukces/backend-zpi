using FluentValidation;

namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed class ResetPasswordCommandValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordCommandValidator()
    {
        RuleFor(command => command.ChallengeId).NotEmpty();
        RuleFor(command => command.Code).NotEmpty().MaximumLength(256);
        RuleFor(command => command.NewPassword).NotNull().Length(8, 128);
    }
}
