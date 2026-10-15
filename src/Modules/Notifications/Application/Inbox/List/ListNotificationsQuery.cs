namespace Zpi.Modules.Notifications.Application.Inbox.List;

public sealed record ListNotificationsQuery(Guid AccountId, ListNotificationsRequest Request);
