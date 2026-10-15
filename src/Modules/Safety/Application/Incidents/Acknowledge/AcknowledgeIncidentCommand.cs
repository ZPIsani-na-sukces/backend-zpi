namespace Zpi.Modules.Safety.Application.Incidents.Acknowledge;

public sealed record AcknowledgeIncidentCommand(Guid AccountId, Guid SeniorId, Guid IncidentId);
