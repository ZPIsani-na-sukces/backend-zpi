using FluentValidation;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Safety.Application.Incidents.List;

public sealed class ListIncidentsRequestValidator : CursorRequestValidator<ListIncidentsRequest>
{
    public ListIncidentsRequestValidator()
    {
        RuleFor(row => row.Type).IsInEnum().When(row => row.Type.HasValue);
        RuleFor(row => row.Status).IsInEnum().When(row => row.Status.HasValue);
        RuleFor(row => row.To).Must((row, to) => !to.HasValue || !row.From.HasValue || to > row.From)
            .WithMessage("To must be later than From.");
    }
}
