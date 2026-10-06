using Zpi.Domain;

namespace Zpi.Modules.Care.Domain;

public sealed class SeniorCareInvitation : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid SeniorId { get; set; }
    public Guid CaretakerId { get; set; }
    public Guid SenderAccountId { get; set; }
    public Guid RecipientAccountId { get; set; }
    public InvitationStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RespondedAt { get; set; }
}
