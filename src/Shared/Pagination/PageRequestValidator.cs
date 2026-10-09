using FluentValidation;

namespace Zpi.Shared.Pagination;

public class PageRequestValidator<T> : AbstractValidator<T> where T : PageRequest
{
    public PageRequestValidator()
    {
        RuleFor(request => request.Limit).InclusiveBetween(1, 100);
        RuleFor(request => request.Page).InclusiveBetween(1, int.MaxValue / 100);
    }
}
