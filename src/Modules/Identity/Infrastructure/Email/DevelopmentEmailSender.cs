using Microsoft.Extensions.Logging;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Identity.Infrastructure.Email;

public sealed class DevelopmentEmailSender(ILogger<DevelopmentEmailSender> logger) : IEmailSender
{
    public bool IsAvailable => true;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        logger.LogInformation("Development email {MessageId} was simulated.", message.MessageId);
        return Task.CompletedTask;
    }
}
