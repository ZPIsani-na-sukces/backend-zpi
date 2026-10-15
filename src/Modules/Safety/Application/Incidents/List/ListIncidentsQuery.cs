namespace Zpi.Modules.Safety.Application.Incidents.List;

public sealed record ListIncidentsQuery(Guid AccountId, Guid SeniorId, ListIncidentsRequest Request);
