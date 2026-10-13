namespace Zpi.Modules.Safety.Application.Telemetry.Upload;

public sealed record UploadTelemetryRequest(IReadOnlyList<TelemetrySample?> Samples);
