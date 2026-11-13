using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Identity.Infrastructure.Email;

public sealed class UnavailableEmailSender : IEmailSender
{
    public bool IsAvailable => false;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken) => throw new EmailDeliveryException();
}
