using FluentValidation;

namespace Zpi.Modules.Identity.Application.Accounts.ChangePassword;

public sealed class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(request => request.CurrentPassword).NotNull().MinimumLength(1);
        RuleFor(request => request.NewPassword).NotNull().Length(8, 128);
    }
}
