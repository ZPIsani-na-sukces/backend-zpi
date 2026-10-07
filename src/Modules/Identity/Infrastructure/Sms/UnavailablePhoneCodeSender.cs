using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class UnavailablePhoneCodeSender : IPhoneCodeSender
{
    public bool IsAvailable => false;

    public Task SendAsync(Guid challengeId, string phoneNumber, string code, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("SMS delivery is not configured.");
}
