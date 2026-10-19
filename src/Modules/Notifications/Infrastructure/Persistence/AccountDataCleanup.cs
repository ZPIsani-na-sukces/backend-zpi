using Microsoft.EntityFrameworkCore;
using Zpi.Domain;

namespace Zpi.Modules.Notifications.Infrastructure.Persistence;

internal sealed class AccountDataCleanup(NotificationsDbContext context) : IAccountDataCleanup
{
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Notifications
            .Where(row => row.RecipientAccountId == accountId || row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await context.PushDeviceTokens
            .Where(row => row.AccountId == accountId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
