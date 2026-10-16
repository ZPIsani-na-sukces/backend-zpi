using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Application.Incidents;

public sealed class IncidentRow
{
    public Guid Id { get; init; }
    public Guid SeniorId { get; init; }
    public Guid DeviceId { get; init; }
    public IncidentType Type { get; init; }
    public IncidentStatus Status { get; init; }
    public DateTimeOffset OccurredAt { get; init; }
    public DateTimeOffset ReceivedAt { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double? AccuracyMeters { get; init; }
    public double? SpeedMps { get; init; }
    public DateTimeOffset? PositionCapturedAt { get; init; }
    public DateTimeOffset? AcknowledgedAt { get; init; }
    public Guid? AcknowledgedByAccountId { get; init; }
    public DateTimeOffset? ResolvedAt { get; init; }
    public Guid? ResolvedByAccountId { get; init; }
    public string? ResolutionNote { get; init; }
    public Guid? ZoneId { get; init; }
    public string? ZoneName { get; init; }
    public string? ZonesJson { get; init; }
    public int? BatteryLevelPercent { get; init; }
}
