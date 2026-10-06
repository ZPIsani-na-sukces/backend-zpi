using Zpi.Domain;

namespace Zpi.Modules.SeniorProfile.Domain;

public sealed class SeniorContactPerson : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public required string DisplayName { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? RelationshipLabel { get; set; }
    public required string PhoneNumber { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid CreatedByAccountId { get; set; }
    public Guid? UpdatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedByAccountId { get; set; }
}
