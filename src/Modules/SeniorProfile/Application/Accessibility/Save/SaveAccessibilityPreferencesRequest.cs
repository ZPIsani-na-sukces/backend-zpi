namespace Zpi.Modules.SeniorProfile.Application.Accessibility.Save;

public sealed record SaveAccessibilityPreferencesRequest(int FontScalePercent, bool HighContrastEnabled, bool LargeButtonsEnabled);
