using Microsoft.EntityFrameworkCore;
using Zpi.Domain;

namespace Zpi.Modules.MedicalRecords.Persistence;

internal sealed class AccountDataCleanup(MedicalRecordsDbContext context) : IAccountDataCleanup
{
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        var records = context.MedicalRecords.Where(row => row.SeniorId == accountId).Select(row => row.Id);
        await context.MedicalFiles.Where(row => records.Contains(row.MedicalRecordId)).ExecuteDeleteAsync(cancellationToken);
        await context.MedicalRecords.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
