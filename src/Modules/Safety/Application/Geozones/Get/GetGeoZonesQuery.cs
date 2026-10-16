namespace Zpi.Modules.Safety.Application.Geozones.Get;

public sealed record GetGeoZonesQuery(Guid AccountId, Guid SeniorId, Guid ZoneId);
