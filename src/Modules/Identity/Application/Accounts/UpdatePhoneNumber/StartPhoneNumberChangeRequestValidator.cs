using FluentValidation;

namespace Zpi.Modules.Identity.Application.Accounts.UpdatePhoneNumber;

public sealed class StartPhoneNumberChangeRequestValidator : AbstractValidator<StartPhoneNumberChangeRequest>
{
    public StartPhoneNumberChangeRequestValidator()
    {
        RuleFor(request => request.PhoneNumber).NotEmpty().Matches(@"\A\+[1-9][0-9]{1,14}\z");
    }
}
