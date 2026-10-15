namespace Zpi.Modules.Safety.Application.Incidents.Resolve;

public sealed record ResolveIncidentCommand(Guid AccountId, Guid SeniorId, Guid IncidentId, string? Note);
