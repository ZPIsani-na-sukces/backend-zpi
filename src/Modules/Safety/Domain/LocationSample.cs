namespace Zpi.Modules.Safety.Domain;

public sealed class LocationSample
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid DeviceId { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? SpeedMps { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
}
