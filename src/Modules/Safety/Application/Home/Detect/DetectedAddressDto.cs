using Zpi.Modules.Safety.Application.Locations;

namespace Zpi.Modules.Safety.Application.Home.Detect;

public sealed record DetectedAddressDto(string DisplayName, CoordinatesDto Coordinates, GeocodedAddressDto Address);

public sealed record GeocodedAddressDto(string? Road, string? HouseNumber, string? Postcode, string? City,
    string? Town, string? Village, string? State, string? Country, string? CountryCode);
