using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.EmailVerification;

public sealed class ConfirmEmailVerificationHandler(IdentityDbContext context, PasswordHashing passwords, TimeProvider clock)
{
    public async Task<AuthenticationResult<bool>> Handle(ConfirmEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var challenge = await context.VerificationChallenges.SingleOrDefaultAsync(row => row.Id == command.ChallengeId, cancellationToken);
        if (challenge is null || challenge.Purpose != ChallengePurpose.EmailVerification || challenge.Channel != ChallengeChannel.Email ||
            challenge.AccountId is null || challenge.ConsumedAt is not null || challenge.InvalidatedAt is not null ||
            challenge.ExpiresAt <= now || challenge.Attempts >= 5)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == challenge.AccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.Active || account.EmailVerifiedAt is not null ||
            account.Email != challenge.Destination)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidState);
        if (passwords.VerifySecret(account, challenge.SecretHash, command.Code) == PasswordVerificationResult.Failed)
        {
            challenge.Attempts++;
            if (challenge.Attempts >= 5) challenge.InvalidatedAt = now;
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
            }
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        }
        challenge.ConsumedAt = now;
        account.EmailVerifiedAt = now;
        account.UpdatedAt = now;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        }
        return AuthenticationResult<bool>.Success(true);
    }
}
