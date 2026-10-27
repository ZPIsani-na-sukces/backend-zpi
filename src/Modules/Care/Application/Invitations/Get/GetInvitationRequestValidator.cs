using FluentValidation;

namespace Zpi.Modules.Care.Application.Invitations.Get;

public sealed class GetInvitationRequestValidator : AbstractValidator<GetInvitationRequest>
{
    public GetInvitationRequestValidator()
    {
        RuleFor(request => request.Code).NotEmpty().Matches("\\A[0-9]{6}\\z");
    }
}
