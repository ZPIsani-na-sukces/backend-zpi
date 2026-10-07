using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Email;

public sealed class UnavailableEmailCodeSender : IEmailCodeSender
{
    public bool IsAvailable => false;

    public Task SendAsync(Guid challengeId, string email, string code, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Email delivery is not configured.");
}
