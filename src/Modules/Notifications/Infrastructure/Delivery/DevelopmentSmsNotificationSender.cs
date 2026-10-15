using Microsoft.Extensions.Logging;
using Zpi.Modules.Notifications.Application.Delivery;

namespace Zpi.Modules.Notifications.Infrastructure.Delivery;

public sealed class DevelopmentSmsNotificationSender(ILogger<DevelopmentSmsNotificationSender> logger) : ISmsNotificationSender
{
    public Task<DeliverySendResult> SendAsync(string idempotencyKey, string phoneNumber, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Development notification SMS {DeliveryKey}: {Message}", idempotencyKey, message);
        return Task.FromResult(new DeliverySendResult(true, "dev:" + idempotencyKey));
    }
}
