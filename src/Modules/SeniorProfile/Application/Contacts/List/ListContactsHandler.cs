using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.List;

public sealed class ListContactsHandler(SeniorProfileDbContext context, ICareFacade care, ContactDtoMapper mapper)
{
    public async Task<SeniorProfileResult<PageDto<ContactDto>>> Handle(ListContactsQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SeniorProfileResult<PageDto<ContactDto>>.Failed(SeniorProfileFailure.ResourceNotFound);
        var rows = context.SeniorContactPersons.AsNoTracking().Where(contact => contact.SeniorId == query.SeniorId &&
            contact.DeletedAt == null && contact.IsActive == query.IsActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().ToUpperInvariant();
            rows = rows.Where(contact => contact.DisplayName.ToUpper().Contains(search) ||
                contact.FirstName != null && contact.FirstName.ToUpper().Contains(search) ||
                contact.LastName != null && contact.LastName.ToUpper().Contains(search) ||
                contact.RelationshipLabel != null && contact.RelationshipLabel.ToUpper().Contains(search));
        }
        var total = await rows.CountAsync(cancellationToken);
        var ordered = query.Sort == "-createdAt" ? rows.OrderByDescending(contact => contact.CreatedAt).ThenByDescending(contact => contact.Id)
            : rows.OrderBy(contact => contact.DisplayName).ThenBy(contact => contact.Id);
        var contacts = await ordered.Skip((query.Page - 1) * query.Limit).Take(query.Limit).ToListAsync(cancellationToken);
        return SeniorProfileResult<PageDto<ContactDto>>.Success(new PageDto<ContactDto>(
            await mapper.MapAsync(contacts, cancellationToken), query.Page, query.Limit, total));
    }
}
