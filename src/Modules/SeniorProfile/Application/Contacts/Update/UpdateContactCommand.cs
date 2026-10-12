namespace Zpi.Modules.SeniorProfile.Application.Contacts.Update;

public sealed record UpdateContactCommand(Guid AccountId, Guid SeniorId, Guid ContactId,
    SaveContactRequest Contact, int ExpectedVersion);
