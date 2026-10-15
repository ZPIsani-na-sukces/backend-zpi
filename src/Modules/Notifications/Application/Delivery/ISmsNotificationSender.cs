namespace Zpi.Modules.Notifications.Application.Delivery;

public interface ISmsNotificationSender
{
    Task<DeliverySendResult> SendAsync(string idempotencyKey, string phoneNumber, string message, CancellationToken cancellationToken);
}
