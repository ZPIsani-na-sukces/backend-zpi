using System.Text.Json.Serialization;

namespace Zpi.Modules.Notifications.Application.Inbox;

public sealed record NotificationDto(Guid NotificationId, Guid? SeniorId, string Type, string Title, string Message,
    DateTimeOffset CreatedAt, DateTimeOffset? ReadAt, NotificationTargetDto Target, IReadOnlyList<NotificationDeliveryDto> Deliveries);
public sealed record NotificationTargetDto(string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? IncidentId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? OccurrenceId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? InvitationId);
public sealed record NotificationDeliveryDto(string Channel, string Status, DateTimeOffset? SentAt, DateTimeOffset? DeliveredAt);
