using Microsoft.Extensions.Logging;
using Zpi.Modules.Identity.Contracts;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class AllowlistedSmsSender(TwilioSmsSender twilio, SmsRecipientAllowlist allowlist,
    ILogger<AllowlistedSmsSender> logger) : ISmsSender
{
    public bool IsAvailable => true;

    public Task<SmsSendResult> SendAsync(string reference, string phoneNumber, string message, CancellationToken cancellationToken)
    {
        if (allowlist.Allows(phoneNumber))
            return twilio.SendAsync(reference, phoneNumber, message, cancellationToken);

        logger.LogInformation("Skipped the SMS for {Reference}: the recipient is not on Twilio:AllowedNumbers, so nothing was sent.",
            reference);
        return Task.FromResult(new SmsSendResult(SmsSendStatus.Sent));
    }
}
