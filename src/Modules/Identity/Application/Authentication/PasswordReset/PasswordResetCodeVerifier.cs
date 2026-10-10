using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed class PasswordResetCodeVerifier(IdentityDbContext context, PasswordHashing passwords, TimeProvider clock)
{
    public async Task<(VerificationChallenge Challenge, Account Account)?> VerifyAsync(Guid challengeId, string code,
        CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var challenge = await context.VerificationChallenges.SingleOrDefaultAsync(row => row.Id == challengeId, cancellationToken);
        if (challenge is null || challenge.Purpose != ChallengePurpose.PasswordReset || challenge.AccountId is null ||
            challenge.ConsumedAt is not null || challenge.InvalidatedAt is not null ||
            challenge.ExpiresAt <= now || challenge.Attempts >= 5)
            return null;
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == challenge.AccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.Active ||
            (challenge.Channel == ChallengeChannel.Sms
                ? account.PhoneVerifiedAt is null || account.PhoneNumber != challenge.Destination
                : account.Email != challenge.Destination))
            return null;
        if (passwords.VerifySecret(account, challenge.SecretHash, code) == PasswordVerificationResult.Failed)
        {
            challenge.Attempts++;
            if (challenge.Attempts >= 5) challenge.InvalidatedAt = now;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return null;
            }
            return null;
        }
        return (challenge, account);
    }
}
