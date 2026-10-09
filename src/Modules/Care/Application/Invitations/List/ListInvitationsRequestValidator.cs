using FluentValidation;
using Zpi.Domain;
using Zpi.Shared.Pagination;
using Zpi.Modules.Care.Domain;

namespace Zpi.Modules.Care.Application.Invitations.List;

public sealed class ListInvitationsRequestValidator : PageRequestValidator<ListInvitationsRequest>
{
    public ListInvitationsRequestValidator()
    {
        RuleFor(request => request.Direction).Must(direction => direction is "received" or "sent")
            .WithMessage("Direction must be received or sent.");
        RuleFor(request => request.Status).Must(status => Enum.GetValues<InvitationStatus>().Any(value => value.ToCamelCase() == status))
            .WithMessage("Invalid invitation status.").When(request => request.Status is not null);
    }
}
