using FluentValidation;

namespace Zpi.Modules.Identity.Application.Accounts.UpdateEmail;

public sealed class UpdateEmailRequestValidator : AbstractValidator<UpdateEmailRequest>
{
    public UpdateEmailRequestValidator()
    {
        RuleFor(request => request.ChallengeId).NotEmpty();
        RuleFor(request => request.Code).NotEmpty().MaximumLength(256);
    }
}
