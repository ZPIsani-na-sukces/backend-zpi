using Microsoft.Extensions.Logging;
using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Email;

public sealed class DevelopmentEmailCodeSender(ILogger<DevelopmentEmailCodeSender> logger) : IEmailCodeSender
{
    public bool IsAvailable => true;

    public Task SendAsync(Guid challengeId, string email, string code, CancellationToken cancellationToken)
    {
        logger.LogInformation("Development email for {Email}, challenge {ChallengeId}: {Code}", email, challengeId, code);
        return Task.CompletedTask;
    }
}
