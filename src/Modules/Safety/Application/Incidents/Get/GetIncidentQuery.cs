namespace Zpi.Modules.Safety.Application.Incidents.Get;

public sealed record GetIncidentQuery(Guid AccountId, Guid SeniorId, Guid IncidentId);
