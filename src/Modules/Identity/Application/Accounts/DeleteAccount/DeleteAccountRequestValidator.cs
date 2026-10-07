using FluentValidation;

namespace Zpi.Modules.Identity.Application.Accounts.DeleteAccount;

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(request => request.Password).NotNull().MinimumLength(1);
    }
}
