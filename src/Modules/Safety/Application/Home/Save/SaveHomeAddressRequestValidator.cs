using FluentValidation;

namespace Zpi.Modules.Safety.Application.Home.Save;

public sealed class SaveHomeAddressRequestValidator : AbstractValidator<SaveHomeAddressRequest>
{
    public SaveHomeAddressRequestValidator()
    {
        RuleFor(row => row.CountryCode).NotEmpty().Matches("^[A-Z]{2}$");
        RuleFor(row => row.PostalCode).NotEmpty().MaximumLength(32);
        RuleFor(row => row.City).NotEmpty().MaximumLength(200);
        RuleFor(row => row.BuildingNumber).NotEmpty().MaximumLength(32);
        RuleFor(row => row.Street).MaximumLength(200);
        RuleFor(row => row.ApartmentNumber).MaximumLength(32);
        RuleFor(row => row.Coordinates).NotNull().SetValidator(new Zpi.Modules.Safety.Application.Geozones.CoordinatesValidator());
    }
}
