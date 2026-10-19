using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Zpi.Domain;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.DeleteAccount;

public sealed class AccountDeletionProcessor(IdentityDbContext context, SessionRevoker revoker,
    IEnumerable<IAccountDataCleanup> cleanupHandlers, TimeProvider clock, ILogger<AccountDeletionProcessor> logger)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow().AddDays(-30);
        var accounts = await context.Accounts.Where(account => account.Status == AccountStatus.PendingDeletion &&
            account.DeletionRequestedAt <= cutoff).OrderBy(account => account.DeletionRequestedAt).Take(100).ToListAsync(cancellationToken);
        foreach (var account in accounts)
        {
            try
            {
                await revoker.RevokeAccountAsync(account.Id, cancellationToken);
                foreach (var handler in cleanupHandlers)
                    await handler.DeleteAsync(account.Id, cancellationToken);
                await context.Accounts.Where(row => row.Id == account.Id && row.Status == AccountStatus.PendingDeletion &&
                    row.DeletionRequestedAt <= cutoff).ExecuteDeleteAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Could not delete account {AccountId}. The next run will retry.", account.Id);
            }
        }
    }
}
