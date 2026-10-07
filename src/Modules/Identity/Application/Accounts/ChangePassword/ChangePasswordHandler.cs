using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.ChangePassword;

public sealed class ChangePasswordHandler(IdentityDbContext context, PasswordHashing passwords,
    SessionRevoker revoker, TimeProvider clock)
{
    public async Task<AuthenticationResult<bool>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken)
    {
        var account = await context.Accounts.SingleAsync(account => account.Id == command.AccountId, cancellationToken);
        if (passwords.Verify(account, command.Passwords.CurrentPassword) == PasswordVerificationResult.Failed)
            return AuthenticationResult<bool>.Failed(AuthenticationFailure.InvalidCredentials);
        account.PasswordHash = passwords.Hash(account, command.Passwords.NewPassword);
        account.SecurityStamp = Guid.CreateVersion7();
        account.UpdatedAt = clock.GetUtcNow();
        await context.SaveChangesAsync(cancellationToken);
        await revoker.RevokeAccountAsync(command.AccountId, cancellationToken);
        return AuthenticationResult<bool>.Success(true);
    }
}
