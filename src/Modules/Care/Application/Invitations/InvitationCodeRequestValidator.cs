using FluentValidation;

namespace Zpi.Modules.Care.Application.Invitations;

public sealed class InvitationCodeRequestValidator : AbstractValidator<InvitationCodeRequest>
{
    public InvitationCodeRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().Matches("\\A[0-9]{6}\\z");
    }
}
