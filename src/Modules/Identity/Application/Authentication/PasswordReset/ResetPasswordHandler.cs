using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Authentication.PasswordReset;

public sealed class ResetPasswordHandler(IdentityDbContext context, PasswordHashing passwords,
    PasswordResetCodeVerifier codes, SessionRevoker revoker, TimeProvider clock)
{
    public async Task<AuthenticationResult<bool>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var verified = await codes.VerifyAsync(command.ChallengeId, command.Code, cancellationToken);
        if (verified is null)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCode);
        var (challenge, account) = verified.Value;
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
