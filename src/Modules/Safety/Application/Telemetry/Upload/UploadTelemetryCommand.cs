namespace Zpi.Modules.Safety.Application.Telemetry.Upload;

public sealed record UploadTelemetryCommand(Guid AccountId, Guid DeviceId, IReadOnlyList<TelemetrySample?> Samples);
