using Zpi.Modules.SeniorProfile.Domain;

namespace Zpi.Modules.SeniorProfile.Application.Contacts.Settings;

public sealed record ContactSettingsDto(Guid SeniorId, bool SeniorEditingLocked, int Version)
{
    public static ContactSettingsDto From(ContactSettings settings) =>
        new(settings.SeniorId, settings.SeniorEditingLocked, settings.Version);
}
