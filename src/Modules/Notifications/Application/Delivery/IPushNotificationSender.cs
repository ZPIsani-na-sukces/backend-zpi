namespace Zpi.Modules.Notifications.Application.Delivery;

public interface IPushNotificationSender
{
    Task<DeliverySendResult> SendAsync(string idempotencyKey, string token, string title, string message,
        Guid notificationId, CancellationToken cancellationToken);
}
