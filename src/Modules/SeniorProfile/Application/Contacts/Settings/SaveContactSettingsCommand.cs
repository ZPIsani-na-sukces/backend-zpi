namespace Zpi.Modules.SeniorProfile.Application.Contacts.Settings;

public sealed record SaveContactSettingsCommand(Guid AccountId, Guid SeniorId, bool SeniorEditingLocked,
    int ExpectedVersion);
