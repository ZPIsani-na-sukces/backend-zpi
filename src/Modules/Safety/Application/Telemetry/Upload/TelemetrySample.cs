using Zpi.Modules.Safety.Application.Locations;

namespace Zpi.Modules.Safety.Application.Telemetry.Upload;

public sealed record TelemetrySample(DateTimeOffset CapturedAt, LocationMeasurement? Location, BatteryMeasurement? Battery);

public sealed record LocationMeasurement(CoordinatesDto? Coordinates, double? AccuracyMeters, double? SpeedMps);

public sealed record BatteryMeasurement(double Level, bool IsCharging);
