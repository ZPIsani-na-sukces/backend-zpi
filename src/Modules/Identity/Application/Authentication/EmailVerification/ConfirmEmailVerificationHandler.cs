using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.EmailVerification;

public sealed class ConfirmEmailVerificationHandler(IdentityDbContext context, VerificationChallengeVerifier challenges, TimeProvider clock)
{
    public async Task<AuthenticationResult<bool>> Handle(ConfirmEmailVerificationCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var challenge = await challenges.FindAsync(command.ChallengeId, ChallengePurpose.EmailVerification, now, cancellationToken);
        if (challenge is null || challenge.Channel != ChallengeChannel.Email)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == challenge.AccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.Active || account.EmailVerifiedAt is not null ||
            account.Email != challenge.Destination)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidState);
        if (!await challenges.VerifyCodeAsync(challenge, account, command.Code, now, cancellationToken))
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);

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
