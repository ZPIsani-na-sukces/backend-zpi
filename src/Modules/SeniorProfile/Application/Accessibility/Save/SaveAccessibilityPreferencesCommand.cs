namespace Zpi.Modules.SeniorProfile.Application.Accessibility.Save;

public sealed record SaveAccessibilityPreferencesCommand(Guid AccountId, Guid SeniorId, int FontScalePercent,
    bool HighContrastEnabled, bool LargeButtonsEnabled, int ExpectedVersion);
