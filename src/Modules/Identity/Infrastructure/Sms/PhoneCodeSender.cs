using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class PhoneCodeSender(ISmsSender sms, IOptions<TwilioOptions> options) : IPhoneCodeSender
{
    public bool IsAvailable => sms.IsAvailable;

    public async Task<PhoneCodeDelivery> SendAsync(Guid challengeId, string phoneNumber, string code, CancellationToken cancellationToken)
    {
        var message = options.Value.MessageTemplate.Replace("{code}", code, StringComparison.Ordinal);
        var result = await sms.SendAsync($"challenge {challengeId}", phoneNumber, message, cancellationToken);
        return result.Status switch
        {
            SmsSendStatus.Sent => PhoneCodeDelivery.Sent,
            SmsSendStatus.InvalidPhoneNumber => PhoneCodeDelivery.InvalidPhoneNumber,
            _ => PhoneCodeDelivery.Unavailable
        };
    }
}
