namespace Zpi.Modules.MedicalRecords.Domain;

public sealed class MedicalAuditEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public Guid? ActorAccountId { get; set; }
    public Guid? RecordId { get; set; }
    public Guid? FileId { get; set; }
    public required string Action { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
}
