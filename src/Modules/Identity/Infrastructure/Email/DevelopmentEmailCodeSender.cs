using Microsoft.Extensions.Logging;
using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Email;

public sealed class DevelopmentEmailCodeSender(ILogger<DevelopmentEmailCodeSender> logger) : IEmailCodeSender
{
    public bool IsAvailable => true;

    public Task SendAsync(EmailCodeDelivery delivery, CancellationToken cancellationToken)
    {
        logger.LogInformation("Development email for {Email}, challenge {ChallengeId}: {Code}", delivery.Email, delivery.ChallengeId, delivery.Code);
        return Task.CompletedTask;
    }
}
