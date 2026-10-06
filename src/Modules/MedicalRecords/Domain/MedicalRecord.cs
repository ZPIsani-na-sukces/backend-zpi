using Zpi.Domain;

namespace Zpi.Modules.MedicalRecords.Domain;

public sealed class MedicalRecord : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public MedicalRecordType RecordType { get; set; }
    public DateOnly DocumentDate { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public Guid? UpdatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedByAccountId { get; set; }
}
