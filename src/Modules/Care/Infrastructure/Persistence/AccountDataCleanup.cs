using Microsoft.EntityFrameworkCore;
using Zpi.Domain;

namespace Zpi.Modules.Care.Infrastructure.Persistence;

internal sealed class AccountDataCleanup(CareDbContext context) : IAccountDataCleanup
{
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.SeniorCareRelationships.Where(row => row.SeniorId == accountId || row.CaretakerId == accountId).ExecuteDeleteAsync(cancellationToken);
        await context.SeniorCareInvitations.Where(row => row.SeniorId == accountId || row.CaretakerId == accountId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
