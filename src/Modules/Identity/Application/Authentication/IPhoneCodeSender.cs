namespace Zpi.Modules.Identity.Application.Authentication;

public interface IPhoneCodeSender
{
    bool IsAvailable { get; }
    Task SendAsync(Guid challengeId, string phoneNumber, string code, CancellationToken cancellationToken);
}
