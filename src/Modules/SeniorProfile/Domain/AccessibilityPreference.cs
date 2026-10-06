using Zpi.Domain;

namespace Zpi.Modules.SeniorProfile.Domain;

public sealed class AccessibilityPreference : VersionedEntity
{
    public Guid SeniorId { get; set; }
    public int FontScalePercent { get; set; } = 100;
    public bool HighContrastEnabled { get; set; }
    public bool LargeButtonsEnabled { get; set; }
    public Guid UpdatedByAccountId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
