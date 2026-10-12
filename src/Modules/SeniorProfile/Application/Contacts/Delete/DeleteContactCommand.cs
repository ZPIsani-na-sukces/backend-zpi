namespace Zpi.Modules.SeniorProfile.Application.Contacts.Delete;

public sealed record DeleteContactCommand(Guid AccountId, Guid SeniorId, Guid ContactId, int ExpectedVersion);
