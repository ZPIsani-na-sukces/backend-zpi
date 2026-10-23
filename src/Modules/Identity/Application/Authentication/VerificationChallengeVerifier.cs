using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication;

public sealed class VerificationChallengeVerifier(IdentityDbContext context, PasswordHashing passwords)
{
    public async Task<VerificationChallenge?> FindAsync(Guid challengeId, ChallengePurpose purpose, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var challenge = await context.VerificationChallenges.SingleOrDefaultAsync(row => row.Id == challengeId, cancellationToken);
        return challenge is null || challenge.Purpose != purpose || challenge.AccountId is null ||
            challenge.ConsumedAt is not null || challenge.InvalidatedAt is not null ||
            challenge.ExpiresAt <= now || challenge.Attempts >= VerificationChallenge.MaximumAttempts
            ? null : challenge;
    }

    public async Task<bool> VerifyCodeAsync(VerificationChallenge challenge, Account account, string code,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (passwords.VerifySecret(account, challenge.SecretHash, code) != PasswordVerificationResult.Failed)
            return true;
        challenge.Attempts++;
        if (challenge.Attempts >= VerificationChallenge.MaximumAttempts) challenge.InvalidatedAt = now;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return false;
        }
        return false;
    }
}
