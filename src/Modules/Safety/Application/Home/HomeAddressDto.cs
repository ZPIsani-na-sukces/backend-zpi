using Zpi.Modules.Safety.Application.Locations;
using Zpi.Modules.Safety.Domain;

namespace Zpi.Modules.Safety.Application.Home;

public sealed record HomeAddressDto(string CountryCode, string PostalCode, string City, string? Street,
    string BuildingNumber, string? ApartmentNumber, CoordinatesDto Coordinates, Guid SeniorId,
    string FormattedAddress, DateTimeOffset UpdatedAt, int Version)
{
    public static HomeAddressDto From(HomeAddress row) => new(row.CountryCode, row.PostalCode, row.City, row.Street,
        row.BuildingNumber, row.ApartmentNumber, new(row.Latitude, row.Longitude), row.SeniorId,
        string.Join(", ", new[] { string.Join(" ", new[] { row.Street, row.BuildingNumber +
            (row.ApartmentNumber is null ? "" : "/" + row.ApartmentNumber) }.Where(value => !string.IsNullOrWhiteSpace(value))),
            row.PostalCode + " " + row.City, row.CountryCode }), row.UpdatedAt, row.Version);
}
