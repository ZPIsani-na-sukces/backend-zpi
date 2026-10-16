namespace Zpi.Modules.Safety.Application.Geozones.Create;

public sealed record CreateGeoZoneCommand(Guid AccountId, Guid SeniorId, SaveGeoZoneRequest Request);
