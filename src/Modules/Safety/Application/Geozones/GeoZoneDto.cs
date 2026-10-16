using Zpi.Modules.Safety.Application.Locations;
using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Application.Geozones;

public sealed record GeoZoneDto(string Name, CoordinatesDto Center, double RadiusMeters, bool IsActive,
    bool SmsOnExitEnabled, Guid ZoneId, Guid SeniorId, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, int Version)
{
    public static GeoZoneDto From(GeoZone row) => new(row.Name, new(row.CenterLatitude, row.CenterLongitude), row.RadiusMeters,
        row.IsActive, row.SmsOnExitEnabled, row.Id, row.SeniorId, row.CreatedAt, row.UpdatedAt, row.Version);
}
