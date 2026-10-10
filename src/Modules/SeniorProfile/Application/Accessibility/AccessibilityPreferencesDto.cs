using Zpi.Modules.SeniorProfile.Domain;

namespace Zpi.Modules.SeniorProfile.Application.Accessibility;

public sealed record AccessibilityPreferencesDto(Guid SeniorId, int FontScalePercent, bool HighContrastEnabled,
    bool LargeButtonsEnabled, DateTimeOffset UpdatedAt, int Version)
{
    internal static AccessibilityPreferencesDto From(AccessibilityPreference preference) => new(preference.SeniorId,
        preference.FontScalePercent, preference.HighContrastEnabled, preference.LargeButtonsEnabled,
        preference.UpdatedAt, preference.Version);
}
