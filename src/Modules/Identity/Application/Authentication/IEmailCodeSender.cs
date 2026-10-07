namespace Zpi.Modules.Identity.Application.Authentication;

public interface IEmailCodeSender
{
    bool IsAvailable { get; }
    Task SendAsync(Guid challengeId, string email, string code, CancellationToken cancellationToken);
}
