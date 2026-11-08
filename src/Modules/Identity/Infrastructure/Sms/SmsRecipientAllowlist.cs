using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Application.Authentication;

namespace Zpi.Modules.Identity.Infrastructure.Sms;

public sealed class SmsRecipientAllowlist(IOptions<TwilioOptions> options) : IPhoneVerificationCodes
{
    public const string FixedCode = "123456";

    private readonly bool restricted = options.Value.RestrictsRecipients;
    private readonly IReadOnlySet<string> allowed = options.Value.AllowedNumberSet;

    public bool Allows(string phoneNumber) => !restricted || allowed.Contains(phoneNumber);

    public string? FixedCodeFor(string phoneNumber) => Allows(phoneNumber) ? null : FixedCode;
}
