using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed class PasswordResetCodeVerifier(IdentityDbContext context, VerificationChallengeVerifier challenges, TimeProvider clock)
{
    public async Task<(VerificationChallenge Challenge, Account Account)?> VerifyAsync(Guid challengeId, string code,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var challenge = await challenges.FindAsync(challengeId, ChallengePurpose.PasswordReset, now, cancellationToken);
        if (challenge is null) return null;
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == challenge.AccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.Active ||
            (challenge.Channel == ChallengeChannel.Sms
                ? account.PhoneVerifiedAt is null || account.PhoneNumber != challenge.Destination
                : account.Email != challenge.Destination))
            return null;
        if (!await challenges.VerifyCodeAsync(challenge, account, code, now, cancellationToken))
            return null;
        return (challenge, account);
    }
}
