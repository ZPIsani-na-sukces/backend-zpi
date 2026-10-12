using FluentValidation;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.List;

public sealed class ListContactsRequestValidator : PageRequestValidator<ListContactsRequest>
{
    public ListContactsRequestValidator()
    {
        RuleFor(request => request.Search).MaximumLength(100);
        RuleFor(request => request.Sort).Must(sort => sort is "displayName" or "-createdAt")
            .WithMessage("Sort must be displayName or -createdAt.");
    }
}
