using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Zpi.Modules.Identity.Application.Authentication;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Authentication;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.DeleteAccount;

public sealed class DeleteAccountHandler(IdentityDbContext context, PasswordHashing passwords,
    SessionRevoker revoker, AccountDeletionProcessor deletion, IOptions<AccountDeletionOptions> options, TimeProvider clock)
{
    public async Task<AuthenticationResult<AccountDeletionDto>> Handle(DeleteAccountCommand command, CancellationToken cancellationToken)
    {
        var account = await context.Accounts.SingleAsync(account => account.Id == command.AccountId, cancellationToken);
        if (passwords.Verify(account, command.Password) == PasswordVerificationResult.Failed)
            return AuthenticationResult<AccountDeletionDto>.Failed(AuthenticationFailure.InvalidCredentials);
        var now = clock.GetUtcNow();
        account.Status = AccountStatus.PendingDeletion;
        account.DeletionRequestedAt = now;
        account.SecurityStamp = Guid.CreateVersion7();
        account.UpdatedAt = now;
        await context.SaveChangesAsync(cancellationToken);
        await revoker.RevokeAccountAsync(command.AccountId, cancellationToken);
        var gracePeriod = options.Value.GracePeriod;
        // Without a grace period the account and its phone number are released now; if that fails, the deletion job retries.
        if (gracePeriod == TimeSpan.Zero)
            await deletion.TryDeleteAsync(command.AccountId, now, cancellationToken);
        return AuthenticationResult<AccountDeletionDto>.Success(new AccountDeletionDto(now + gracePeriod));
    }
}
