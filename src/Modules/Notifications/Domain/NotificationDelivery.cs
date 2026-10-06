using Zpi.Domain;

namespace Zpi.Modules.Notifications.Domain;

public sealed class NotificationDelivery : VersionedEntity
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid NotificationId { get; set; }
    public DeliveryChannel Channel { get; set; }
    public DeliveryStatus Status { get; set; }
    public int AttemptCount { get; set; }
    public DateTimeOffset? NextAttemptAt { get; set; }
    public string? ProviderMessageId { get; set; }
    public string? LastErrorCode { get; set; }
    public DateTimeOffset? SentAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
}
