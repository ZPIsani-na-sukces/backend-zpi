using Zpi.Domain;

namespace Zpi.Modules.Safety.Domain;

public sealed class SosIncident : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public Guid DeviceId { get; set; }
    public IncidentStatus Status { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset ReceivedAt { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public double? AccuracyMeters { get; set; }
    public double? SpeedMps { get; set; }
    public DateTimeOffset? PositionCapturedAt { get; set; }
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public Guid? AcknowledgedByAccountId { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public Guid? ResolvedByAccountId { get; set; }
    public string? ResolutionNote { get; set; }
}
