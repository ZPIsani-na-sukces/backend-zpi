using Microsoft.EntityFrameworkCore;
using Zpi.Domain;

namespace Zpi.Modules.SeniorProfile.Infrastructure.Persistence;

internal sealed class AccountDataCleanup(SeniorProfileDbContext context) : IAccountDataCleanup
{
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SeniorContactPersons.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await context.AccessibilityPreferences.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
