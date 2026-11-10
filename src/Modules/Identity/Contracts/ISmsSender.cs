namespace Zpi.Modules.Identity.Contracts;

public interface ISmsSender
{
    bool IsAvailable { get; }
    Task<SmsSendResult> SendAsync(string reference, string phoneNumber, string message, CancellationToken cancellationToken);
}
