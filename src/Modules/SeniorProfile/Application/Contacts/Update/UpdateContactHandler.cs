using Microsoft.EntityFrameworkCore;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.Update;

public sealed class UpdateContactHandler(SeniorProfileDbContext context, ContactWriteAccess access,
    ContactDtoMapper mapper, TimeProvider clock)
{
    public async Task<SeniorProfileResult<ContactDto>> Handle(UpdateContactCommand command, CancellationToken cancellationToken)
    {
        if (await access.CheckAsync(command.AccountId, command.SeniorId, cancellationToken) is { } failure)
            return SeniorProfileResult<ContactDto>.Failed(failure);
        var contact = await context.SeniorContactPersons.SingleOrDefaultAsync(row => row.Id == command.ContactId &&
            row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken);
        if (contact is null) return SeniorProfileResult<ContactDto>.Failed(SeniorProfileFailure.ResourceNotFound);
        if (contact.Version != command.ExpectedVersion)
            return SeniorProfileResult<ContactDto>.Failed(SeniorProfileFailure.VersionMismatch);
        command.Contact.ApplyTo(contact);
        contact.UpdatedAt = clock.GetUtcNow();
        contact.UpdatedByAccountId = command.AccountId;
        await context.SaveChangesAsync(cancellationToken);
        return SeniorProfileResult<ContactDto>.Success(await mapper.MapAsync(contact, cancellationToken));
    }
}
