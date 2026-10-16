using Zpi.Modules.Safety.Application.Locations;

namespace Zpi.Modules.Safety.Application.Home.Save;

public sealed record SaveHomeAddressRequest(string CountryCode, string PostalCode, string City, string? Street,
    string BuildingNumber, string? ApartmentNumber, CoordinatesDto Coordinates);
