using FluentValidation;
using System.Text.Json.Serialization;

namespace Zpi.Modules.Safety.Application.Home.Detect;

public sealed record DetectAddressRequest([property: JsonRequired] double Latitude, [property: JsonRequired] double Longitude);

public sealed class DetectAddressRequestValidator : AbstractValidator<DetectAddressRequest>
{
    public DetectAddressRequestValidator()
    {
        RuleFor(row => row.Latitude).Must(value => double.IsFinite(value) && value is >= -90 and <= 90);
        RuleFor(row => row.Longitude).Must(value => double.IsFinite(value) && value is >= -180 and <= 180);
    }
}
