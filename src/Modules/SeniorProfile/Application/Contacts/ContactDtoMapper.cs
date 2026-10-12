using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.SeniorProfile.Domain;

namespace Zpi.Modules.SeniorProfile.Application.Contacts;

public sealed class ContactDtoMapper(IIdentityFacade identity)
{
    public async Task<ContactDto> MapAsync(SeniorContactPerson contact, CancellationToken cancellationToken)
    {
        var accounts = await identity.GetAccountSummariesAsync([contact.CreatedByAccountId], cancellationToken);
        return ContactDto.From(contact, accounts.GetValueOrDefault(contact.CreatedByAccountId));
    }

    public async Task<IReadOnlyList<ContactDto>> MapAsync(IReadOnlyList<SeniorContactPerson> contacts,
        CancellationToken cancellationToken)
    {
        var accounts = await identity.GetAccountSummariesAsync(contacts.Select(contact => contact.CreatedByAccountId).Distinct().ToArray(),
            cancellationToken);
        return contacts.Select(contact => ContactDto.From(contact, accounts.GetValueOrDefault(contact.CreatedByAccountId))).ToList();
    }
}
