using FluentValidation;

namespace Zpi.Modules.Care.Application.Invitations.Create;

public sealed class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(request => request.RecipientIdentifier).NotEmpty().MaximumLength(254);
        RuleFor(request => request.RecipientIdentifier).Matches(@"^\+[1-9][0-9]{1,14}$")
            .When(request => request.RecipientIdentifier?.StartsWith('+') == true);
        RuleFor(request => request.RecipientIdentifier).EmailAddress()
            .When(request => request.RecipientIdentifier?.StartsWith('+') != true);
    }
}
