namespace Zpi.Modules.Safety.Application.Geozones.Update;

public sealed record UpdateGeoZoneCommand(Guid AccountId, Guid SeniorId, Guid ZoneId, int ExpectedVersion, SaveGeoZoneRequest Request);
