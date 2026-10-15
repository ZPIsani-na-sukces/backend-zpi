using Zpi.Modules.Notifications.Domain;

namespace Zpi.Modules.Notifications.Contracts;

public sealed record NotificationEvent(Guid EventId, Guid RecipientAccountId, Guid? SeniorId, NotificationType Type,
    string Title, string Message, NotificationTargetKind TargetKind, Guid TargetId, DateTimeOffset? ExpiresAt);
