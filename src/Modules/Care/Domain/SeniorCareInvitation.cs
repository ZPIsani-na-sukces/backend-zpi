using Zpi.Domain;

namespace Zpi.Modules.Care.Domain;

public sealed class SeniorCareInvitation : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SenderAccountId { get; set; }
    public string Code { get; set; } = "";
    public InvitationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public Guid? RecipientAccountId { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
    public Guid? RelationshipId { get; set; }
}
