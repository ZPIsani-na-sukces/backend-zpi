using Zpi.Domain;

namespace Zpi.Modules.Care.Domain;

public sealed class SeniorCareRelationship : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public Guid CaretakerId { get; set; }
    public RelationshipStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RemovedAt { get; set; }
    public Guid? RemovedByAccountId { get; set; }
}
