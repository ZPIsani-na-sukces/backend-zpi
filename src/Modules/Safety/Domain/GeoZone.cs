using Zpi.Domain;

namespace Zpi.Modules.Safety.Domain;

public sealed class GeoZone : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public required string Name { get; set; }
    public double CenterLatitude { get; set; }
    public double CenterLongitude { get; set; }
    public double RadiusMeters { get; set; }
    public bool IsActive { get; set; } = true;
    public bool SmsOnExitEnabled { get; set; } = true;
    public Guid CreatedByAccountId { get; set; }
    public Guid? UpdatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedByAccountId { get; set; }
}
