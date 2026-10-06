using Zpi.Domain;

namespace Zpi.Modules.Safety.Domain;

public sealed class HomeAddress : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public required string CountryCode { get; set; }
    public required string PostalCode { get; set; }
    public required string City { get; set; }
    public string? Street { get; set; }
    public required string BuildingNumber { get; set; }
    public string? ApartmentNumber { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public Guid? UpdatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedByAccountId { get; set; }
}
