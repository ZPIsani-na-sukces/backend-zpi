namespace Zpi.Modules.SeniorProfile.Application.Contacts.Get;

public sealed record GetContactQuery(Guid AccountId, Guid SeniorId, Guid ContactId);
