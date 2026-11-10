using Microsoft.Extensions.Logging;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class DevelopmentSmsSender(ILogger<DevelopmentSmsSender> logger) : ISmsSender
{
    public bool IsAvailable => true;

    public Task<SmsSendResult> SendAsync(string reference, string phoneNumber, string message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Development SMS for {Reference} to {PhoneNumber}: {Message}", reference, phoneNumber, message);
        return Task.FromResult(new SmsSendResult(SmsSendStatus.Sent));
    }
}
