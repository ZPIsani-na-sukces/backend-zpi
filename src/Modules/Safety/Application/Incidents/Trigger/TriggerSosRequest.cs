namespace Zpi.Modules.Safety.Application.Incidents.Trigger;

public sealed record TriggerSosRequest(Guid DeviceId, DateTimeOffset TriggeredAt, IncidentPositionDto? Position = null);
