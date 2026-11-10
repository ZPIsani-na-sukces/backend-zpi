using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class UnavailableSmsSender : ISmsSender
{
    public bool IsAvailable => false;

    public Task<SmsSendResult> SendAsync(string reference, string phoneNumber, string message, CancellationToken cancellationToken) =>
        Task.FromResult(new SmsSendResult(SmsSendStatus.Unavailable));
}
