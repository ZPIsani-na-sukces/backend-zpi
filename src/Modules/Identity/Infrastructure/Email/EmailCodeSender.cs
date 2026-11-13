using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Contracts;
using Zpi.Modules.Identity.Infrastructure.Email.Templates;

namespace Zpi.Modules.Identity.Infrastructure.Email;

public sealed class EmailCodeSender(IEmailSender sender) : IEmailCodeSender
{
    public bool IsAvailable => sender.IsAvailable;

    public Task SendAsync(EmailCodeDelivery delivery, CancellationToken cancellationToken) =>
        sender.SendAsync(AuthenticationEmailTemplate.Create(delivery), cancellationToken);
}
