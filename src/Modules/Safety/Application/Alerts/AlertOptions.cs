namespace Zpi.Modules.Safety.Application.Alerts;

public sealed class AlertOptions
{
    public const string SectionName = "Safety:Alerts";
    public TimeSpan Freshness { get; set; } = TimeSpan.FromMinutes(5);
    public TimeSpan Confirmation { get; set; } = TimeSpan.FromSeconds(30);
    public int LowBatteryPercent { get; set; } = 20;
    public int BatteryRearmPercent { get; set; } = 30;

    internal bool IsFresh(DateTimeOffset capturedAt, DateTimeOffset now) => capturedAt <= now && capturedAt >= now - Freshness;
}
