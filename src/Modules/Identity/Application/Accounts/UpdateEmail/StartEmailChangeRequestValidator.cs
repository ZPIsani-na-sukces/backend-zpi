using FluentValidation;

namespace Zpi.Modules.Identity.Application.Accounts.UpdateEmail;

public sealed class StartEmailChangeRequestValidator : AbstractValidator<StartEmailChangeRequest>
{
    public StartEmailChangeRequestValidator()
    {
        RuleFor(request => request.Email).NotEmpty().EmailAddress().MaximumLength(254);
    }
}
