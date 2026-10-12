using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.Get;

public sealed class GetContactHandler(SeniorProfileDbContext context, ICareFacade care, ContactDtoMapper mapper)
{
    public async Task<SeniorProfileResult<ContactDto>> Handle(GetContactQuery query, CancellationToken cancellationToken)
    {
        if (!await care.CanAccessSeniorAsync(query.AccountId, query.SeniorId, cancellationToken))
            return SeniorProfileResult<ContactDto>.Failed(SeniorProfileFailure.ResourceNotFound);
        var contact = await context.SeniorContactPersons.AsNoTracking().SingleOrDefaultAsync(row =>
            row.Id == query.ContactId && row.SeniorId == query.SeniorId && row.DeletedAt == null, cancellationToken);
        return contact is null ? SeniorProfileResult<ContactDto>.Failed(SeniorProfileFailure.ResourceNotFound)
            : SeniorProfileResult<ContactDto>.Success(await mapper.MapAsync(contact, cancellationToken));
    }
}
