namespace Zpi.Modules.Identity.Contracts;

public interface IEmailSender
{
    bool IsAvailable { get; }
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
