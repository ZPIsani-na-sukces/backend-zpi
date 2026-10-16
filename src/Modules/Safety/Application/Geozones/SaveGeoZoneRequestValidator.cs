using FluentValidation;
using Zpi.Modules.Safety.Application.Locations;

namespace Zpi.Modules.Safety.Application.Geozones;

public sealed class CoordinatesValidator : AbstractValidator<CoordinatesDto>
{
    public CoordinatesValidator()
    {
        RuleFor(row => row.Latitude).Must(double.IsFinite).InclusiveBetween(-90, 90);
        RuleFor(row => row.Longitude).Must(double.IsFinite).InclusiveBetween(-180, 180);
    }
}

public sealed class SaveGeoZoneRequestValidator : AbstractValidator<SaveGeoZoneRequest>
{
    public SaveGeoZoneRequestValidator()
    {
        RuleFor(row => row.Name).NotEmpty().MaximumLength(100);
        RuleFor(row => row.Center).NotNull().SetValidator(new CoordinatesValidator());
        RuleFor(row => row.RadiusMeters).Must(double.IsFinite).InclusiveBetween(50, 10000);
    }
}
