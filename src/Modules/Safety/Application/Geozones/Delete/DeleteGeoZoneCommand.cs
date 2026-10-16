namespace Zpi.Modules.Safety.Application.Geozones.Delete;

public sealed record DeleteGeoZoneCommand(Guid AccountId, Guid SeniorId, Guid ZoneId, int ExpectedVersion);
