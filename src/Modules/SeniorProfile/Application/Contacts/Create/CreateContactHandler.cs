using Zpi.Modules.Care.Contracts;
using Zpi.Modules.SeniorProfile.Domain;
using Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.Create;

public sealed class CreateContactHandler(SeniorProfileDbContext context, ICareFacade care,
    ContactDtoMapper mapper, TimeProvider clock)
{
    public async Task<SeniorProfileResult<ContactDto>> Handle(CreateContactCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return SeniorProfileResult<ContactDto>.Failed(SeniorProfileFailure.ResourceNotFound);
        var now = clock.GetUtcNow();
        var contact = new SeniorContactPerson
        {
            SeniorId = command.SeniorId, DisplayName = command.Contact.DisplayName, PhoneNumber = command.Contact.PhoneNumber,
            CreatedByAccountId = command.AccountId, CreatedAt = now, UpdatedAt = now
        };
        command.Contact.ApplyTo(contact);
        context.SeniorContactPersons.Add(contact);
        await context.SaveChangesAsync(cancellationToken);
        return SeniorProfileResult<ContactDto>.Success(await mapper.MapAsync(contact, cancellationToken));
    }
}
