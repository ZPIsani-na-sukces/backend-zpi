using FluentValidation;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.Routines.Application.Occurrences.List;

public sealed class ListOccurrencesRequestValidator : CursorRequestValidator<ListOccurrencesRequest>
{
    public ListOccurrencesRequestValidator(TimeProvider clock)
    {
        RuleFor(request => request.From).NotNull();
        RuleFor(request => request.To).NotNull();
        RuleFor(request => request.To).Must((request, to) => request.From is null || to is null ||
            to > request.From && to - request.From <= TimeSpan.FromDays(31))
            .WithMessage("The range must be ordered and no longer than 31 days.");
        RuleFor(request => request.To).Must(to => to is null || to <= clock.GetUtcNow().AddDays(30))
            .WithMessage("Future occurrences are available up to 30 days ahead.");
        RuleFor(request => request.Type).IsInEnum();
        RuleFor(request => request.Status).IsInEnum();
        RuleFor(request => request.TaskId).NotEqual(Guid.Empty);
    }
}
