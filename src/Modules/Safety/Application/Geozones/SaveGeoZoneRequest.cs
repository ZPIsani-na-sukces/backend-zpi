using Zpi.Modules.Safety.Application.Locations;

namespace Zpi.Modules.Safety.Application.Geozones;

public sealed record SaveGeoZoneRequest(string Name, CoordinatesDto Center, double RadiusMeters,
    bool IsActive = true, bool SmsOnExitEnabled = true);
