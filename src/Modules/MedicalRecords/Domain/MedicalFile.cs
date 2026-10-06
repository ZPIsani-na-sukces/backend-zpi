namespace Zpi.Modules.MedicalRecords.Domain;

public sealed class MedicalFile
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid MedicalRecordId { get; set; }
    public required string ObjectKey { get; set; }
    public required string OriginalName { get; set; }
    public required string MimeType { get; set; }
    public long SizeBytes { get; set; }
    public required string ChecksumSha256 { get; set; }
    public Guid CreatedByAccountId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
