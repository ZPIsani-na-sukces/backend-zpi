using FluentValidation;

namespace Zpi.Modules.SeniorProfile.Application.Contacts;

public sealed class SaveContactRequestValidator : AbstractValidator<SaveContactRequest>
{
    public SaveContactRequestValidator()
    {
        RuleFor(request => request.DisplayName).NotEmpty().MaximumLength(100);
        RuleFor(request => request.FirstName).MaximumLength(100);
        RuleFor(request => request.LastName).MaximumLength(100);
        RuleFor(request => request.RelationshipLabel).MaximumLength(100);
        RuleFor(request => request.PhoneNumber).NotEmpty().Matches(@"\A\+[1-9][0-9]{1,14}\z");
    }
}
