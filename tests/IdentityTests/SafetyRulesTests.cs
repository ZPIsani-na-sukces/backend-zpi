using Zpi.Modules.Safety.Application.Home.Detect;
using Zpi.Modules.Safety.Application.Incidents;
using Zpi.Modules.Safety.Application.Incidents.Trigger;
using Zpi.Modules.Safety.Application.Telemetry.Upload;

namespace Zpi.IdentityTests;

public sealed class SafetyRulesTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(90, -180, true)]
    [InlineData(-90, 180, true)]
    [InlineData(-91, 21, false)]
    [InlineData(52, -181, false)]
    [InlineData(52, 181, false)]
    [InlineData(double.NaN, 21, false)]
    [InlineData(52, double.PositiveInfinity, false)]
    public void Address_detection_only_accepts_finite_coordinates_within_wgs84_bounds(
        double latitude, double longitude, bool valid)
    {
        var result = new DetectAddressRequestValidator().Validate(new DetectAddressRequest(latitude, longitude));
        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public void Telemetry_indicates_the_reason_for_rejecting_each_invalid_measurement()
    {
        var validator = new TelemetrySampleValidator(new TestClock());
        var location = new LocationMeasurement(new(52, 21), null, null);
        var sample = new TelemetrySample(Now, location, null);
        Assert.True(validator.Validate(sample).IsValid);
        Assert.True(validator.Validate(new TelemetrySample(Now, null, new(0, false))).IsValid);

        var invalid = new (TelemetrySample Sample, string Code)[]
        {
            (new(Now, null, new(-1, false)), "emptySample"),
            (sample with { Location = location with { Coordinates = null } }, "invalidCoordinates"),
            (sample with { Location = location with { Coordinates = new(double.NaN, 21) } }, "invalidCoordinates"),
            (sample with { Location = location with { Coordinates = new(52, double.PositiveInfinity) } }, "invalidCoordinates"),
            (sample with { Location = location with { Coordinates = new(52, -181) } }, "invalidCoordinates"),
            (sample with { Location = location with { AccuracyMeters = double.NaN } }, "invalidAccuracy"),
            (sample with { Location = location with { SpeedMps = double.NegativeInfinity } }, "invalidSpeed"),
            (sample with { Battery = new(double.NaN, false) }, "invalidBattery"),
            (sample with { CapturedAt = Now.AddMinutes(5).AddMilliseconds(1) }, "invalidCapturedAt"),
            (sample with { CapturedAt = Now.AddHours(-24).AddMilliseconds(-1) }, "invalidCapturedAt")
        };
        foreach (var (input, code) in invalid)
            Assert.Equal(code, Assert.Single(validator.Validate(input).Errors).ErrorCode);
    }

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(0.0, 0.0, true)]
    [InlineData(double.NaN, 0.0, false)]
    [InlineData(0.0, double.PositiveInfinity, false)]
    [InlineData(-1.0, 0.0, false)]
    [InlineData(0.0, -1.0, false)]
    public void Sos_position_allows_unavailable_sensor_values_and_rejects_non_finite_or_negative_data(
        double? accuracy, double? speed, bool valid)
    {
        var validator = new TriggerSosRequestValidator(new TestClock());
        var position = new IncidentPositionDto(new(52, 21), accuracy, speed, Now.AddMinutes(-1));
        Assert.Equal(valid, validator.Validate(new TriggerSosRequest(Guid.CreateVersion7(), Now, position)).IsValid);
    }

    private sealed class TestClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
