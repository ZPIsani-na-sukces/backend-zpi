using Zpi.Modules.Safety.Application.Locations;

namespace Zpi.Modules.Safety.Application.Incidents;

public sealed record IncidentPositionDto(CoordinatesDto Coordinates, double? AccuracyMeters, double? SpeedMps, DateTimeOffset CapturedAt);
