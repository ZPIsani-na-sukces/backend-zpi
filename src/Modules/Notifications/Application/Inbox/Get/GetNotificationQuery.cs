namespace Zpi.Modules.Notifications.Application.Inbox.Get;

public sealed record GetNotificationQuery(Guid AccountId, Guid NotificationId);
