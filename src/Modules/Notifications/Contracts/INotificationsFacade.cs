namespace Zpi.Modules.Notifications.Contracts;

public interface INotificationsFacade
{
    Task EnqueueAsync(string source, NotificationEvent notification, CancellationToken cancellationToken);
}
