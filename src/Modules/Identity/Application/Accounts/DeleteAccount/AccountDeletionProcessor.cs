using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Zpi.Domain;
using Zpi.Modules.Identity.Application.Sessions;
using Zpi.Modules.Identity.Domain;
using Zpi.Modules.Identity.Infrastructure.Persistence;

namespace Zpi.Modules.Identity.Application.Accounts.DeleteAccount;

public sealed class AccountDeletionProcessor(IdentityDbContext context, SessionRevoker revoker,
    IEnumerable<IAccountDataCleanup> cleanupHandlers, IOptions<AccountDeletionOptions> options, TimeProvider clock,
    ILogger<AccountDeletionProcessor> logger)
{
    public async Task ProcessAsync(CancellationToken cancellationToken)
    {
        var cutoff = clock.GetUtcNow() - options.Value.GracePeriod;
        var accountIds = await context.Accounts.Where(account => account.Status == AccountStatus.PendingDeletion &&
            account.DeletionRequestedAt <= cutoff).OrderBy(account => account.DeletionRequestedAt).Take(100)
            .Select(account => account.Id).ToListAsync(cancellationToken);
        foreach (var accountId in accountIds)
            await TryDeleteAsync(accountId, cutoff, cancellationToken);
    }

    // Removes an account whose deletion was requested at or before the cutoff. A failure is logged and the next run retries.
    public async Task<bool> TryDeleteAsync(Guid accountId, DateTimeOffset cutoff, CancellationToken cancellationToken)
    {
        try
        {
            await revoker.RevokeAccountAsync(accountId, cancellationToken);
            foreach (var handler in cleanupHandlers)
                await handler.DeleteAsync(accountId, cancellationToken);
            return await context.Accounts.Where(row => row.Id == accountId && row.Status == AccountStatus.PendingDeletion &&
                row.DeletionRequestedAt <= cutoff).ExecuteDeleteAsync(cancellationToken) > 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Could not delete account {AccountId}. The next run will retry.", accountId);
            return false;
        }
    }
}
