using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;
using Zpi.Shared.Pagination;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.List;

public sealed class ListContactsHandler(SeniorProfileDbContext context, ICareFacade care, ContactDtoMapper mapper)
{
    public async Task<SeniorProfileResult<CursorPageDto<ContactDto>>> Handle(ListContactsQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SeniorProfileResult<CursorPageDto<ContactDto>>.Failed(SeniorProfileFailure.ResourceNotFound);
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
        var pagination = rows.Paginate(query.Cursor, new { query.AccountId, query.SeniorId, query.Search, query.IsActive, query.Sort });
        if (query.Sort == "-createdAt") pagination.OrderBy(contact => contact.CreatedAt, true).OrderBy(contact => contact.Id, true);
        else pagination.OrderBy(contact => contact.DisplayName).OrderBy(contact => contact.Id);
        var contacts = await pagination.Take(query.Limit).ToListAsync(cancellationToken);
        return SeniorProfileResult<CursorPageDto<ContactDto>>.Success(new CursorPageDto<ContactDto>(
            await mapper.MapAsync(contacts.Take(query.Limit).ToList(), cancellationToken), pagination.NextCursor(contacts, query.Limit)));
    }
}
