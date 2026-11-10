using FluentValidation;

namespace Zpi.Modules.Identity.Application.Accounts.UpdatePhoneNumber;

public sealed class UpdatePhoneNumberRequestValidator : AbstractValidator<UpdatePhoneNumberRequest>
{
    public UpdatePhoneNumberRequestValidator()
    {
        RuleFor(request => request.ChallengeId).NotEmpty();
        RuleFor(request => request.Code).NotEmpty().Matches(@"\A[0-9]{6}\z");
    }
}
