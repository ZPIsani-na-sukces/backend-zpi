using System.Globalization;
using System.Security.Cryptography;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;

namespace Zpi.Modules.Identity.Application.Authentication;

internal static class VerificationChallengeFactory
{
    public static (VerificationChallenge Challenge, string Code) Create(Account account, ChallengePurpose purpose,
        ChallengeChannel channel, string destination, PasswordHashing passwords, DateTimeOffset now,
        VerificationTicket? ticket = null, string? fixedCode = null)
    {
        var code = fixedCode ?? (channel == ChallengeChannel.Sms
            ? RandomNumberGenerator.GetInt32(1_000_000).ToString("D6", CultureInfo.InvariantCulture)
            : TokenService.CreateSecret());
        return (new VerificationChallenge
        {
            AccountId = account.Id,
            TicketId = ticket?.Id,
            Purpose = purpose,
            Channel = channel,
            Destination = destination,
            SecretHash = passwords.Hash(account, code),
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(5),
            ResendAfter = now.AddSeconds(60)
        }, code);
    }
}
