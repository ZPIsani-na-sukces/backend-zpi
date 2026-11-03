using FluentValidation;

namespace Zpi.Modules.Safety.Application.Telemetry.Upload;

public sealed class TelemetrySampleValidator : AbstractValidator<TelemetrySample>
{
    public TelemetrySampleValidator(TimeProvider clock)
    {
        RuleFor(sample => sample).Must(sample => sample.Location is not null || sample.Battery is { Level: >= 0 })
            .WithErrorCode("emptySample");
        RuleFor(sample => sample.CapturedAt).Must(capturedAt =>
                capturedAt >= clock.GetUtcNow().AddHours(-24) && capturedAt <= clock.GetUtcNow().AddMinutes(5))
            .WithErrorCode("invalidCapturedAt");
        When(sample => sample.Location is not null, () =>
        {
            RuleFor(sample => sample.Location!.Coordinates).Must(coordinates => coordinates is not null &&
                double.IsFinite(coordinates.Latitude) && coordinates.Latitude is >= -90 and <= 90 &&
                double.IsFinite(coordinates.Longitude) && coordinates.Longitude is >= -180 and <= 180)
                .WithErrorCode("invalidCoordinates");
            RuleFor(sample => sample.Location!.AccuracyMeters).Must(value => value is null || double.IsFinite(value.Value) && value.Value >= 0)
                .WithErrorCode("invalidAccuracy");
            RuleFor(sample => sample.Location!.SpeedMps).Must(value => value is null || double.IsFinite(value.Value))
                .WithErrorCode("invalidSpeed");
        });
        RuleFor(sample => sample.Battery!.Level).Must(level => double.IsFinite(level) && level <= 1)
            .WithErrorCode("invalidBattery").When(sample => sample.Battery is not null);
    }
}
