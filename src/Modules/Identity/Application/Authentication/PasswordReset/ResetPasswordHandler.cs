using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed class ResetPasswordHandler(IdentityDbContext context, PasswordHashing passwords,
    SessionRevoker revoker, TimeProvider clock)
{
    public async Task<AuthenticationResult<bool>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var challenge = await context.VerificationChallenges.SingleOrDefaultAsync(row => row.Id == command.ChallengeId, cancellationToken);
        if (challenge is null || challenge.Purpose != ChallengePurpose.PasswordReset || challenge.AccountId is null ||
            challenge.ConsumedAt is not null || challenge.InvalidatedAt is not null ||
            challenge.ExpiresAt <= now || challenge.Attempts >= 5)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        var account = await context.Accounts.SingleOrDefaultAsync(row => row.Id == challenge.AccountId, cancellationToken);
        if (account is null || account.Status != AccountStatus.Active ||
            (challenge.Channel == ChallengeChannel.Sms
                ? account.PhoneVerifiedAt is null || account.PhoneNumber != challenge.Destination
                : account.EmailVerifiedAt is null || account.Email != challenge.Destination))
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
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
        var otherChallenges = await context.VerificationChallenges.Where(row => row.AccountId == account.Id &&
                row.Purpose == ChallengePurpose.PasswordReset && row.Id != challenge.Id &&
                row.ConsumedAt == null && row.InvalidatedAt == null).ToListAsync(cancellationToken);
        foreach (var other in otherChallenges) other.InvalidatedAt = now;
        challenge.ConsumedAt = now;
        account.PasswordHash = passwords.Hash(account, command.NewPassword);
        account.SecurityStamp = Guid.CreateVersion7();
        account.UpdatedAt = now;
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        }
        await revoker.RevokeAccountAsync(account.Id, cancellationToken);
        return AuthenticationResult<bool>.Success(true);
    }
}
