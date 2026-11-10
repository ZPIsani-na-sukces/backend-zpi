using Zpi.Modules.Notifications.Application.Delivery;

namespace Zpi.Modules.Notifications.Infrastructure.Delivery;

public sealed class UnavailablePushNotificationSender : IPushNotificationSender
{
    public Task<DeliverySendResult> SendAsync(string idempotencyKey, string token, string title, string message,
        Guid notificationId, CancellationToken cancellationToken) =>
        Task.FromResult(new DeliverySendResult(false, ErrorCode: "providerNotConfigured"));
}
