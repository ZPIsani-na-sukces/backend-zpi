using FluentValidation;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Notifications.Application.Inbox.List;

public sealed class ListNotificationsRequestValidator : PageRequestValidator<ListNotificationsRequest>
{
    public ListNotificationsRequestValidator()
    {
        RuleFor(row => row.SeniorId).NotEqual(Guid.Empty).When(row => row.SeniorId.HasValue);
        RuleFor(row => row.Type).IsInEnum().When(row => row.Type.HasValue);
        RuleFor(row => row.To).Must((row, to) => !row.From.HasValue || !to.HasValue || to > row.From)
            .WithMessage("To must be later than From.");
    }
}
