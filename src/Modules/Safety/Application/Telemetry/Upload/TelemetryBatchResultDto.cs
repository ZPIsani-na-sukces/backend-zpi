namespace Zpi.Modules.Safety.Application.Telemetry.Upload;

public sealed record TelemetryBatchResultDto(DateTimeOffset ReceivedAt, IReadOnlyList<TelemetrySampleResultDto> Results);

public sealed record TelemetrySampleResultDto(string Result, string? ErrorCode);
