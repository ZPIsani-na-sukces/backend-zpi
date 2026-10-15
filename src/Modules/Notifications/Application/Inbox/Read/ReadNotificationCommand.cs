namespace Zpi.Modules.Notifications.Application.Inbox.Read;

public sealed record ReadNotificationCommand(Guid AccountId, Guid NotificationId);
