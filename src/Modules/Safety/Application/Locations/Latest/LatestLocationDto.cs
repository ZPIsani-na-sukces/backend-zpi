namespace Zpi.Modules.Safety.Application.Locations.Latest;

public sealed record LatestLocationDto(Guid DeviceId, CoordinatesDto Coordinates, double? AccuracyMeters,
    double? SpeedMps, DateTimeOffset CapturedAt, DateTimeOffset ReceivedAt);
