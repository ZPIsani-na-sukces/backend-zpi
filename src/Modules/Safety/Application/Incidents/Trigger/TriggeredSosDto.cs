namespace Zpi.Modules.Safety.Application.Incidents.Trigger;

public sealed record TriggeredSosDto(IncidentDto Incident, bool Created);
