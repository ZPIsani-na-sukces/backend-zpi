using FluentValidation;
using Zpi.Modules.Identity.Domain;

namespace Zpi.Modules.Identity.Application.Authentication.Login;

public sealed class LoginCommandValidator : AbstractValidator<LoginCommand>
{
    public LoginCommandValidator()
    {
        RuleFor(request => request.Identifier).NotEmpty().MaximumLength(254);
        RuleFor(request => request.Password).NotNull().MinimumLength(1);
        RuleFor(request => request.ClientType).IsInEnum();
    }
}
