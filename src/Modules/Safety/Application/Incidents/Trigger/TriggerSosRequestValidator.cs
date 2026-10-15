using FluentValidation;

namespace Zpi.Modules.Safety.Application.Incidents.Trigger;

public sealed class TriggerSosRequestValidator : AbstractValidator<TriggerSosRequest>
{
    public TriggerSosRequestValidator(TimeProvider clock)
    {
        RuleFor(row => row.DeviceId).NotEmpty();
        RuleFor(row => row.TriggeredAt).Must(value => value >= clock.GetUtcNow().AddDays(-30) && value <= clock.GetUtcNow().AddMinutes(5))
            .WithMessage("TriggeredAt must be within the last 30 days and at most 5 minutes ahead.");
        RuleFor(row => row.Position!).SetValidator(new IncidentPositionValidator(clock)).When(row => row.Position is not null);
        RuleFor(row => row.Position!.CapturedAt).Must((row, value) => value <= row.TriggeredAt)
            .When(row => row.Position is not null).WithMessage("Position must not be newer than the SOS trigger.");
    }
}

public sealed class IncidentPositionValidator : AbstractValidator<IncidentPositionDto>
{
    public IncidentPositionValidator(TimeProvider clock)
    {
        RuleFor(row => row.Coordinates).NotNull();
        When(row => row.Coordinates is not null, () =>
        {
            RuleFor(row => row.Coordinates.Latitude).InclusiveBetween(-90, 90);
            RuleFor(row => row.Coordinates.Longitude).InclusiveBetween(-180, 180);
        });
        RuleFor(row => row.AccuracyMeters).Must(value => !value.HasValue || double.IsFinite(value.Value) && value >= 0)
            .WithMessage("Accuracy must be finite and non-negative.");
        RuleFor(row => row.SpeedMps).Must(value => !value.HasValue || double.IsFinite(value.Value) && value >= 0)
            .WithMessage("Speed must be finite and non-negative.");
        RuleFor(row => row.CapturedAt).Must(value => value >= clock.GetUtcNow().AddDays(-30) && value <= clock.GetUtcNow().AddMinutes(5))
            .WithMessage("Position must be within the last 30 days and at most 5 minutes ahead.");
    }
}
