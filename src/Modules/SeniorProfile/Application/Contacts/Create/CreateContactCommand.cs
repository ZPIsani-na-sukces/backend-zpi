namespace Zpi.Modules.SeniorProfile.Application.Contacts.Create;

public sealed record CreateContactCommand(Guid AccountId, Guid SeniorId, SaveContactRequest Contact);
