namespace Zpi.Modules.Safety.Domain;

public sealed class BatterySample
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DeviceId { get; set; }
    public int BatteryLevelPercent { get; set; }
    public bool IsCharging { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
