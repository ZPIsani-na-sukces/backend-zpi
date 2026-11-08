using Microsoft.Extensions.Logging;
using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class AllowlistedPhoneCodeSender(TwilioPhoneCodeSender twilio, SmsRecipientAllowlist allowlist,
    ILogger<AllowlistedPhoneCodeSender> logger) : IPhoneCodeSender
{
    public bool IsAvailable => true;

    public Task<PhoneCodeDelivery> SendAsync(Guid challengeId, string phoneNumber, string code, CancellationToken cancellationToken)
    {
        if (allowlist.Allows(phoneNumber))
            return twilio.SendAsync(challengeId, phoneNumber, code, cancellationToken);

        logger.LogInformation(
            "Skipped SMS for challenge {ChallengeId}: recipient is not on Twilio:AllowedNumbers. Phone verification uses the fixed code {FixedCode}.",
            challengeId, SmsRecipientAllowlist.FixedCode);
        return Task.FromResult(PhoneCodeDelivery.Sent);
    }
}
