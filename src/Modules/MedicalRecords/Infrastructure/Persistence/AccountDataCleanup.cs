using Microsoft.EntityFrameworkCore;
using Zpi.Domain;
using Zpi.Modules.MedicalRecords.Application.Files;

namespace Zpi.Modules.MedicalRecords.Persistence;

internal sealed class AccountDataCleanup(MedicalRecordsDbContext context, IMedicalFileStorage storage) : IAccountDataCleanup
{
    public async Task DeleteAsync(Guid accountId, CancellationToken cancellationToken)
    {
        var records = context.MedicalRecords
            .Where(row => row.SeniorId == accountId)
            .Select(row => row.Id);
        var keys = await context.MedicalFiles
            .Where(row => records.Contains(row.MedicalRecordId) && row.PurgedAt == null)
            .Select(row => row.ObjectKey)
            .ToListAsync(cancellationToken);
        foreach (var key in keys) await storage.DeleteAsync(key, cancellationToken);
        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        await context.MedicalAuditEntries.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await context.MedicalAuditEntries.Where(row => row.ActorAccountId == accountId)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.ActorAccountId, (Guid?)null), cancellationToken);
        await context.MedicalRecords.Where(row => row.SeniorId == accountId).ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
