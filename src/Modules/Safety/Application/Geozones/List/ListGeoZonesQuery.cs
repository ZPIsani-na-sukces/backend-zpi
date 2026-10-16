namespace Zpi.Modules.Safety.Application.Geozones.List;

public sealed record ListGeoZonesQuery(Guid AccountId, Guid SeniorId, bool? IsActive);
