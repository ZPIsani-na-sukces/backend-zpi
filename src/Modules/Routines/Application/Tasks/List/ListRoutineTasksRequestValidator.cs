using FluentValidation;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Routines.Application.Tasks.List;

public sealed class ListRoutineTasksRequestValidator : CursorRequestValidator<ListRoutineTasksRequest>
{
    public ListRoutineTasksRequestValidator()
    {
        RuleFor(request => request.Type).IsInEnum();
        RuleFor(request => request.Search).MaximumLength(100);
    }
}
