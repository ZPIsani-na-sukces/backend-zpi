using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Notifications.Application.Delivery;

namespace Zpi.Modules.Notifications.Infrastructure.Delivery;

public sealed class SmsNotificationSender(ISmsSender sms) : ISmsNotificationSender
{
    public async Task<DeliverySendResult> SendAsync(string idempotencyKey, string phoneNumber, string message, CancellationToken cancellationToken)
    {
        if (!sms.IsAvailable) return new DeliverySendResult(false, ErrorCode: "providerNotConfigured", Retryable: false);
        var result = await sms.SendAsync($"notification delivery {idempotencyKey}", phoneNumber, message, cancellationToken);
        
        return result.Status switch
        {
            SmsSendStatus.Sent => new DeliverySendResult(true, result.ProviderMessageId),
            SmsSendStatus.InvalidPhoneNumber => new DeliverySendResult(false, ErrorCode: "invalidPhoneNumber", Retryable: false),
            _ => new DeliverySendResult(false, ErrorCode: "providerUnavailable")
        };
    }
}
