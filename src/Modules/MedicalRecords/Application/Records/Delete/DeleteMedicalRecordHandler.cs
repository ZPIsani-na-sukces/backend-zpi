using Microsoft.EntityFrameworkCore;
using Zpi.Modules.Care.Contracts;
using Zpi.Modules.MedicalRecords.Application.Audit;
using Zpi.Modules.MedicalRecords.Persistence;

namespace Zpi.Modules.MedicalRecords.Application.Records.Delete;

public sealed class DeleteMedicalRecordHandler(MedicalRecordsDbContext context, ICareFacade care, MedicalAudit audit, TimeProvider clock)
{
    public async Task<MedicalResult<bool>> Handle(DeleteMedicalRecordCommand command, CancellationToken cancellationToken)
    {
        if (!await care.HasActiveRelationshipAsync(command.AccountId, command.SeniorId, cancellationToken))
            return MedicalResult<bool>.Failed(MedicalFailure.ResourceNotFound);
        var row = await context.MedicalRecords.SingleOrDefaultAsync(row => row.Id == command.RecordId && row.SeniorId == command.SeniorId && row.DeletedAt == null, cancellationToken);
        if (row is null) return MedicalResult<bool>.Failed(MedicalFailure.ResourceNotFound);
        if (row.Version != command.Version) return MedicalResult<bool>.Failed(MedicalFailure.VersionMismatch);
        var now = clock.GetUtcNow();
        row.DeletedAt = now;
        row.DeletedByAccountId = command.AccountId;
        var files = await context.MedicalFiles.Where(file => file.MedicalRecordId == row.Id && file.DeletedAt == null)
            .ToListAsync(cancellationToken);
        foreach (var file in files) file.DeletedAt = now;
        row.UpdatedAt = now;
        row.UpdatedByAccountId = command.AccountId;
        context.Entry(row).Property(record => record.UpdatedAt).IsModified = true;
        audit.Add(command.AccountId, command.SeniorId, row.Id, null, MedicalActions.Delete);
        await context.SaveChangesAsync(cancellationToken);
        return MedicalResult<bool>.Success(true);
    }
}
