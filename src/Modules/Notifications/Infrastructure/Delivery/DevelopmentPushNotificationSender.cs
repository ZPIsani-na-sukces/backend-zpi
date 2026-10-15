using Microsoft.Extensions.Logging;
using Zpi.Modules.Notifications.Application.Delivery;

namespace Zpi.Modules.Notifications.Infrastructure.Delivery;

public sealed class DevelopmentPushNotificationSender(ILogger<DevelopmentPushNotificationSender> logger) : IPushNotificationSender
{
    public Task<DeliverySendResult> SendAsync(string idempotencyKey, string token, string title, string message,
        Guid notificationId, CancellationToken cancellationToken)
    {
        logger.LogInformation("Development push {DeliveryKey}, notification {NotificationId}: {Title} — {Message}",
            idempotencyKey, notificationId, title, message);
        return Task.FromResult(new DeliverySendResult(true, "dev:" + idempotencyKey));
    }
}
