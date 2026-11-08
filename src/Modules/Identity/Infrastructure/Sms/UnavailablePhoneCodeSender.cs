using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class UnavailablePhoneCodeSender : IPhoneCodeSender
{
    public bool IsAvailable => false;

    public Task<PhoneCodeDelivery> SendAsync(Guid challengeId, string phoneNumber, string code, CancellationToken cancellationToken) =>
        Task.FromResult(PhoneCodeDelivery.Unavailable);
}
