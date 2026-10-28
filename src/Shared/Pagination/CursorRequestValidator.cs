using FluentValidation;

namespace Zpi.Shared.Pagination;

public class CursorRequestValidator<T> : AbstractValidator<T> where T : CursorRequest
{
    public CursorRequestValidator()
    {
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request.Cursor).Must(CursorQuery.IsValid).WithMessage("Invalid cursor.")
            .When(request => request.Cursor is not null);
    }
}
