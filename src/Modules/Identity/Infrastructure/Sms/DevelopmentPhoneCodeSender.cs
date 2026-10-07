using Microsoft.Extensions.Logging;
using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class DevelopmentPhoneCodeSender(ILogger<DevelopmentPhoneCodeSender> logger) : IPhoneCodeSender
{
    public bool IsAvailable => true;

    public Task SendAsync(Guid challengeId, string phoneNumber, string code, CancellationToken cancellationToken)
    {
        logger.LogInformation("Development SMS for {PhoneNumber}, challenge {ChallengeId}: {Code}", phoneNumber, challengeId, code);
        return Task.CompletedTask;
    }
}
