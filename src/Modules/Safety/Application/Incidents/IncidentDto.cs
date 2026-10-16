using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Safety.Application.Incidents;

public sealed record IncidentDto(Guid IncidentId, Guid SeniorId, Guid DeviceId, string Type, string Status,
    DateTimeOffset OccurredAt, DateTimeOffset ReceivedAt, IncidentPositionDto? Position, IncidentDetailsDto Details,
    DateTimeOffset? AcknowledgedAt, AccountSummary? AcknowledgedBy, DateTimeOffset? ResolvedAt,
    AccountSummary? ResolvedBy, string? ResolutionNote);

public sealed record IncidentDetailsDto(string Kind)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public Guid? ZoneId { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? ZoneName { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<Zpi.Modules.Safety.Application.Alerts.DepartedZoneDto>? Zones { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public int? BatteryLevelPercent { get; init; }
}
