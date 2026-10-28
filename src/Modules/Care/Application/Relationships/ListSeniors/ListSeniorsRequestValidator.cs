using FluentValidation;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Care.Application.Relationships.ListSeniors;

public sealed class ListSeniorsRequestValidator : CursorRequestValidator<ListSeniorsRequest>
{
    public ListSeniorsRequestValidator()
    {
        RuleFor(request => request.Search).MaximumLength(100);
    }
}
