using Zpi.Domain;

namespace Zpi.Modules.Notifications.Domain;

public sealed class Notification : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid IntegrationEventId { get; set; }
    public Guid RecipientAccountId { get; set; }
    public Guid? SeniorId { get; set; }
    public NotificationType Type { get; set; }
    public required string Title { get; set; }
    public required string Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public NotificationTargetKind TargetKind { get; set; }
    public Guid TargetId { get; set; }
}
