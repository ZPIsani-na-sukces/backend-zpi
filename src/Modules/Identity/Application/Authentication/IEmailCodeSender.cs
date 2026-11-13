namespace Zpi.Modules.Identity.Application.Authentication;

public interface IEmailCodeSender
{
    bool IsAvailable { get; }
    Task SendAsync(EmailCodeDelivery delivery, CancellationToken cancellationToken);
}
